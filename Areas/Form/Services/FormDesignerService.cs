using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Helper;
using System.Net;
using System.Text.RegularExpressions;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.Options;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.SqlHelper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace DcMateH5Api.Areas.Form.Services;

public class FormDesignerService : IFormDesignerService
{
    private readonly SqlConnection _con;
    private readonly IConfiguration _configuration;
    private readonly ISchemaService _schemaService;
    private readonly ITransactionService _transactionService;
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IDropdownSqlSyncService _dropdownSqlSyncService;
    private readonly IReadOnlyList<string> _relationColumnSuffixes;

    public FormDesignerService(
        SQLGenerateHelper sqlHelper,
        SqlConnection connection,
        IConfiguration configuration,
        ISchemaService schemaService,
        IDropdownSqlSyncService dropdownSqlSyncService,
        IOptions<FormSettings> formSettings,
        ITransactionService transactionService)
    {
        _con = connection;
        _configuration = configuration;
        _schemaService = schemaService;
        _sqlHelper = sqlHelper;
        _dropdownSqlSyncService = dropdownSqlSyncService;
        _transactionService = transactionService;
        
        _excludeColumns = _configuration.GetSection("DropdownSqlSettings:ExcludeColumns").Get<List<string>>() ?? new();
        _requiredColumns = _configuration.GetSection("FormDesignerSettings:RequiredColumns").Get<List<string>>() ?? new();
        var resolvedSettings = formSettings?.Value ?? new FormSettings();
        _relationColumnSuffixes = resolvedSettings.GetRelationColumnSuffixesOrDefault();
    }

    private readonly List<string> _excludeColumns;
    private readonly List<string> _requiredColumns;
    
    #region Public API
    
    /// <summary>
    /// 取得 FORM_FIELD_MASTER 列表（可依 SchemaType 與關鍵字模糊查詢）
    /// </summary>
    public Task<List<FormFieldMasterDto>> GetFormMasters(
        FormFunctionType functionType,
        string? q,
        CancellationToken ct)
    {
        // 建立查詢條件：
        // 1. 功能類型需符合指定的 FormFunctionType
        // 2. 排除已被標記為刪除的資料（Soft Delete）
        var where = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.FUNCTION_TYPE!, functionType)
            .AndNotDeleted();

        // 若有輸入關鍵字，則加上表單名稱模糊查詢條件
        if (!string.IsNullOrWhiteSpace(q))
        {
            where.AndLike(x => x.FORM_NAME, q);
        }
        
        var res = _sqlHelper.SelectWhereAsync(where, ct);
        return res;
    }

    public Task UpdateFormName(UpdateFormNameViewModel model, CancellationToken ct)
    {
        return _sqlHelper.UpdateById<FormFieldMasterDto>(model.ID)
            .Set(x => x.FORM_NAME, model.FORM_NAME)
            .ExecuteAsync(ct);
    }
    
    /// <summary>
    /// 取得單一主表設定
    /// </summary>
    /// <param name="id">主表ID</param>
    /// <param name="ct">取消權杖</param>
    /// <returns></returns>
    private Task<FormFieldMasterDto?> GetFormMasterAsync(Guid? id, CancellationToken ct)
    {
        if (id == null) return Task.FromResult<FormFieldMasterDto?>(null);

        var where = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, id)
            .AndNotDeleted();
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    /// <summary>
    /// 刪除單一表單主檔（硬刪除），並一併刪除其欄位設定、驗證規則、下拉設定與選項。
    /// 使用 TransactionService 確保全有全無，避免刪一半留下殘骸。
    /// </summary>
    public void DeleteFormMaster(Guid id)
    {
        _transactionService.WithTransaction(tx =>
        {
            _con.Execute(
                Sql.DeleteFormMaster,
                new { id },
                transaction: tx,
                commandTimeout: 30);
        });
    }
    
    /// <summary>
    /// 根據 functionType 取得 Form Designer 首頁所需資料（用於前端建立樹狀結構/左側欄位樹）。
    ///
    /// 核心目的：
    /// 依「表單功能類型」回傳對應的資料集（Base/Detail/Mapping/View），讓前端可以：
    /// - 建立欄位樹狀結構
    /// - 顯示不同表（主檔/明細/關聯/檢視）的欄位清單
    ///
    /// 功能類型對應資料來源：
    /// - 主檔維護（MasterMaintenance）
    ///   - 必要：Base + View
    ///   - 回傳：BaseFields、ViewFields（Detail/Mapping 會是 null）
    ///
    /// - 一對多維護（MasterDetailMaintenance）
    ///   - 必要：Base + Detail + View
    ///   - 回傳：BaseFields、DetailFields、ViewFields（Mapping 會是 null）
    ///
    /// - 多對多維護（MultipleMappingMaintenance）
    ///   - 必要：Base + Detail + Mapping
    ///   - View：可選（有設定才回傳）
    ///  - 回傳：BaseFields、DetailFields、MappingFields（ViewFields 視情況回傳）
    ///
    /// 重要防呆：
    /// - 若主檔的 FUNCTION_TYPE 與呼叫者傳入的 functionType 不一致，代表前端進錯模組
    ///   → 直接丟可讀的 Domain 例外（避免錯頁面操作到錯結構）
    /// - 若必要的表設定缺失（Base/Detail/Mapping/View），代表設定不完整
    ///   → 直接丟可讀例外，讓維護者知道缺哪個設定欄位
    /// </summary>
    public async Task<FormDesignerIndexViewModel> GetFormDesignerIndexViewModel(
        FormFunctionType functionType, 
        Guid? id, 
        CancellationToken ct)
    {
        // 1) 取得表單主檔設定（FormHeader）
        // - 這是整個 Designer 組裝的根資料
        // - 後續 Base/Detail/Mapping/View 的表名與表 SID 都從這裡拿
        var master = await GetFormMasterAsync(id, ct) ?? throw new Exception("查無主檔。");

        // 2) 防呆：避免「進錯功能模組」卻載入別的表單設定
        // - 例如：表單實際是 MasterDetail，但你用 MasterMaintenance 模組打開
        // - 會導致前端樹狀結構/CRUD 行為全部錯
        if (master.FUNCTION_TYPE.HasValue && master.FUNCTION_TYPE != functionType)
            throw new Exception("表單功能模組與表單型態不一致，請使用對應的維護模組。");
        
        // 3) 防呆：Base 表為所有功能類型的必備資料來源
        // - 無 Base 就沒有主資料可維護/欄位可設計
        if (string.IsNullOrWhiteSpace(master.BASE_TABLE_NAME) || master.BASE_TABLE_ID is null)
            throw new Exception("缺少主檔（Base）表設定：請檢查 BASE_TABLE_NAME / BASE_TABLE_ID。");

        // 4) 準備回傳物件（先放 FormHeader，其餘欄位由 functionType 決定）
        // - 這裡先給 null!，代表「稍後一定會被填值（或明確維持 null）」避免 nullable 雜訊
        var result = new FormDesignerIndexViewModel
        {
            FormHeader = master,
            BaseFields = null!,
            DetailFields = null!,
            ViewFields = null!,
            MappingFields = null!
        };

        // 5) BaseFields：所有 functionType 都需要
        // - TableSchemaQueryType.OnlyTable 表示抓「實體表欄位」（主檔維護來源）
        result.BaseFields = await GetFieldsByTableName(
            master.BASE_TABLE_NAME,
            master.BASE_TABLE_ID.Value,
            TableSchemaQueryType.OnlyTable);

        // 6) 依不同功能類型，組裝不同的欄位集合
        FormFieldListViewModel? viewFields;
        switch (functionType)
        {
            case FormFunctionType.MasterMaintenance:
                // 主檔維護：必須有 View（列表/查詢通常來自 View）
                if (string.IsNullOrWhiteSpace(master.VIEW_TABLE_NAME) || master.VIEW_TABLE_ID is null)
                    throw new Exception("缺少檢視表（View）表設定：請檢查 VIEW_TABLE_NAME / VIEW_TABLE_ID。");

                // 主檔維護不需要 Detail / Mapping
                result.DetailFields = null!;
                result.MappingFields = null!;

                // ViewFields：抓檢視表欄位（通常含 join 欄位、顯示用欄位）
                result.ViewFields = await GetFieldsByTableName(
                    master.VIEW_TABLE_NAME,
                    master.VIEW_TABLE_ID.Value,
                    TableSchemaQueryType.OnlyView);
                
                break;
            
            case FormFunctionType.MasterDetailMaintenance:
                // 一對多：必須有 Detail + View
                if (string.IsNullOrWhiteSpace(master.DETAIL_TABLE_NAME) || master.DETAIL_TABLE_ID is null)
                    throw new Exception("缺少明細表設定：請檢查 DETAIL_TABLE_NAME / DETAIL_TABLE_ID。");
                
                if (string.IsNullOrWhiteSpace(master.VIEW_TABLE_NAME) || master.VIEW_TABLE_ID is null)
                    throw new Exception("缺少檢視表（View）表設定：請檢查 VIEW_TABLE_NAME / VIEW_TABLE_ID。");

                // 一對多不需要 Mapping
                result.MappingFields = null!;

                // DetailFields：抓明細表欄位
                result.DetailFields = await GetFieldsByTableName(
                    master.DETAIL_TABLE_NAME,
                    master.DETAIL_TABLE_ID.Value,
                    TableSchemaQueryType.OnlyDetail);
                
                // ViewFields：抓檢視表欄位（列表/查詢/顯示通常用 View）
                result.ViewFields = await GetFieldsByTableName(
                    master.VIEW_TABLE_NAME,
                    master.VIEW_TABLE_ID.Value,
                    TableSchemaQueryType.OnlyView);
                
                break;
            
            case FormFunctionType.MultipleMappingMaintenance:
                // 多對多：必須有 Detail + Mapping；View 可選
                if (string.IsNullOrWhiteSpace(master.DETAIL_TABLE_NAME) || master.DETAIL_TABLE_ID is null)
                    throw new Exception("缺少對應的明細表設定：請檢查 DETAIL_TABLE_NAME / DETAIL_TABLE_ID。");
                if (string.IsNullOrWhiteSpace(master.MAPPING_TABLE_NAME) || master.MAPPING_TABLE_ID is null)
                    throw new Exception("缺少關聯表設定：請檢查 MAPPING_TABLE_NAME / MAPPING_TABLE_ID。");

                // DetailFields：多對多通常會有一個「被關聯的明細資料」來源
                result.DetailFields = await GetFieldsByTableName(
                    master.DETAIL_TABLE_NAME,
                    master.DETAIL_TABLE_ID.Value,
                    TableSchemaQueryType.OnlyDetail);

                // MappingFields：中介表（關聯表）欄位
                result.MappingFields = await GetFieldsByTableName(
                    master.MAPPING_TABLE_NAME,
                    master.MAPPING_TABLE_ID.Value,
                    TableSchemaQueryType.OnlyMapping);

                // ViewFields（可選）：若有設定 view，則額外提供給前端顯示用
                // - 不強制，因為多對多不一定需要 view
                if (!string.IsNullOrWhiteSpace(master.VIEW_TABLE_NAME) && master.VIEW_TABLE_ID is not null)
                {
                    result.ViewFields = await GetFieldsByTableName(
                        master.VIEW_TABLE_NAME,
                        master.VIEW_TABLE_ID.Value,
                        TableSchemaQueryType.OnlyView);
                }
                break;
            
            default:
                throw new InvalidOperationException("不支援的功能模組型態。");
        }
        return result;
    }
    
    /// <summary>
    /// 依名稱關鍵字查詢資料表或檢視表清單。
    /// </summary>
    /// <param name="tableName">表名稱關鍵字或樣式</param>
    /// <param name="schemaType">搜尋目標類型（主表或檢視表）</param>
    /// <returns>符合條件的表名稱集合</returns>
    public List<string> SearchTables(string? tableName, TableSchemaQueryType schemaType)
    {
        // 1) 輸入驗證（防 SQL Injection / 非預期字元）
        // - 僅允許英數、底線與點
        // - 點是為了支援 schema.table 的輸入格式
        if (!string.IsNullOrWhiteSpace(tableName) &&
            !Regex.IsMatch(tableName, @"^[A-Za-z0-9_\.]+$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("tableName 含非法字元");
        }

        // 2) 解析可能的 schema.name 輸入
        // - 若有「.」，視為 schema + table name
        // - 若無「.」，僅當作 table name 關鍵字
        string? schema = null, name = null;
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            var parts = tableName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2) { schema = parts[0]; name = parts[1]; }
            else { name = parts[0]; }
        }

        // 決定 VIEW 或 BASE TABLE
        var tableType = schemaType == TableSchemaQueryType.OnlyView ? "VIEW" : "BASE TABLE";

        const string sql = @"/**/
SELECT TABLE_SCHEMA + '.' + TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
ORDER BY TABLE_SCHEMA, TABLE_NAME;";

        var param = new { tableType, schema, name };
        return _con.Query<string>(sql, param).ToList();
    }
    
    public Guid GetOrCreateFormMasterId(FormFieldMasterDto model)
    {
        var sql = @"SELECT ID FROM FORM_FIELD_MASTER WHERE ID = @id";
        var res = _con.QueryFirstOrDefault<Guid?>(sql, new { id = model.ID });

        if (res.HasValue)
            return res.Value;

        var insertId = model.ID == Guid.Empty ? Guid.NewGuid() : model.ID;
        static bool HasValue(string? s) => !string.IsNullOrWhiteSpace(s);
        
        _con.Execute(@"
        INSERT INTO FORM_FIELD_MASTER
    (ID, FORM_NAME, STATUS, SCHEMA_TYPE,
     BASE_TABLE_NAME, VIEW_TABLE_NAME, DETAIL_TABLE_NAME, MAPPING_TABLE_NAME,
     BASE_TABLE_ID,  VIEW_TABLE_ID,  DETAIL_TABLE_ID, MAPPING_TABLE_ID,
     FUNCTION_TYPE, IS_DELETE)
    VALUES
    (@ID, @FORM_NAME, @STATUS, @SCHEMA_TYPE,
     @BASE_TABLE_NAME, @VIEW_TABLE_NAME, @DETAIL_TABLE_NAME, @MAPPING_TABLE_NAME,
     @BASE_TABLE_ID,  @VIEW_TABLE_ID,  @DETAIL_TABLE_ID, @MAPPING_TABLE_ID,
     @FUNCTION_TYPE, 0);", new
        {
            ID = insertId,
            model.FORM_NAME,
            model.STATUS,
            model.SCHEMA_TYPE,
            model.BASE_TABLE_NAME,
            model.VIEW_TABLE_NAME,
            model.DETAIL_TABLE_NAME,
            model.MAPPING_TABLE_NAME,

            BASE_TABLE_ID   = HasValue(model.BASE_TABLE_NAME)   ? insertId : (Guid?)null,
            VIEW_TABLE_ID   = HasValue(model.VIEW_TABLE_NAME)   ? insertId : (Guid?)null,
            DETAIL_TABLE_ID = HasValue(model.DETAIL_TABLE_NAME) ? insertId : (Guid?)null,
            MAPPING_TABLE_ID = HasValue(model.MAPPING_TABLE_NAME) ? insertId : (Guid?)null,

            FUNCTION_TYPE = model.FUNCTION_TYPE,
        });

        return insertId;
    }
    
    /// <summary>
    /// 根據資料表名稱，取得所有欄位資訊並合併 欄位設定、驗證資訊。
    /// </summary>
    /// <param name="tableName">使用者輸入的表名稱</param>
    /// <param name="formMasterId"></param>
    /// <param name="schemaType"></param>
    /// <returns></returns>
    public async Task<FormFieldListViewModel> GetFieldsByTableName( string tableName, Guid? formMasterId, TableSchemaQueryType schemaType )
    {
        var columns = GetTableSchema(tableName);
        if (columns.Count == 0) return new();

        var configs = await GetFieldConfigs(tableName, formMasterId);
        var requiredFieldIds = GetRequiredFieldIds();

        // 查 PK
        var pk = _schemaService.GetPrimaryKeyColumns(tableName);
        
        // 4) 逐欄位組裝 ViewModel
        var fields = new List<FormFieldViewModel>(columns.Count);
        foreach (var col in columns)
        {
            var columnName = col.COLUMN_NAME;
            var dataType   = col.DATA_TYPE;

            // 4-1) 取對應設定（有就用設定 ID，沒有就產新的暫時 ID）
            var hasCfg  = configs.TryGetValue(columnName, out var cfg);
            var fieldId = hasCfg ? cfg!.ID : Guid.NewGuid();

            var vm = new FormFieldViewModel
            {
                ID                          = fieldId,
                FORM_FIELD_MASTER_ID        = formMasterId ?? Guid.Empty,
                TableName                   = tableName,
                COLUMN_NAME                 = columnName,
                DATA_TYPE                   = dataType,
                CONTROL_TYPE                = cfg?.CONTROL_TYPE, // 可能被 policy 改為 null
                CONTROL_TYPE_WHITELIST      = FormFieldHelper.GetControlTypeWhitelist(dataType),
                QUERY_COMPONENT_TYPE_WHITELIST = FormFieldHelper.GetQueryConditionTypeWhitelist(dataType),
                IS_REQUIRED                 = cfg?.IS_REQUIRED ?? false, // bool 不可 null，用預設
                IS_EDITABLE                 = cfg?.IS_EDITABLE ?? true,  // bool 不可 null，用預設
                IS_VALIDATION_RULE          = requiredFieldIds.Contains(fieldId),
                IS_PK                       = pk.Contains(columnName),
                QUERY_DEFAULT_VALUE         = cfg?.QUERY_DEFAULT_VALUE,
                SchemaType                  = schemaType,
                QUERY_COMPONENT             = cfg?.QUERY_COMPONENT ?? QueryComponentType.None,
                QUERY_CONDITION             = cfg?.QUERY_CONDITION,
                CAN_QUERY                   = cfg?.CAN_QUERY ?? false
            };

            // 4-2) 依 schemaType 做欄位「置空/預設化」策略
            ApplySchemaPolicy(vm, schemaType);

            fields.Add(vm);
        }
        // 用設定檔過濾
        // .Where(f => !_excludeColumns.Any(ex => 
        //     f.COLUMN_NAME.Contains(ex, StringComparison.OrdinalIgnoreCase)))
        // .ToList();
        
        var masterId = formMasterId ?? configs.Values.FirstOrDefault()?.FORM_FIELD_MASTER_ID ?? Guid.Empty;

        var result = new FormFieldListViewModel
        {
            Fields = fields,
        };

        return result;
    }

    /// <summary>
    /// 依 SchemaType 套用欄位置空/預設化策略：
    /// - OnlyView：視為純查詢，不允許編輯 → 把編輯相關設為不可編輯/無控制型態
    /// - OnlyTable：視為純維運，不提供查詢條件 → 把查詢相關清空/關閉
    /// - Both/Default：保留原設定
    /// </summary>
    private static void ApplySchemaPolicy(FormFieldViewModel f, TableSchemaQueryType schemaType)
    {
        switch (schemaType)
        {
            case TableSchemaQueryType.OnlyTable:
            case TableSchemaQueryType.OnlyMapping:
                // 純表維運：查詢相關關閉/清空
                f.CAN_QUERY = null;
                f.QUERY_COMPONENT = null;
                f.QUERY_CONDITION = null;
                f.QUERY_COMPONENT_TYPE_WHITELIST = null;
                f.QUERY_DEFAULT_VALUE = null;
                break;
            
            case TableSchemaQueryType.OnlyView:
                // 只查不改：編輯控制改為 null、不可編輯、必填關閉
                f.IS_EDITABLE = null;
                f.IS_REQUIRED = null;
                f.IS_VALIDATION_RULE = null;
                f.FIELD_ORDER = null;
                f.CONTROL_TYPE = null;
                f.CONTROL_TYPE_WHITELIST = null;
                break;
        }
    }
    
    /// <summary>
    /// 依欄位設定 ID 取得單一欄位的完整設定資訊。
    ///
    /// 核心用途：
    /// - 提供表單設計器「欄位編輯」畫面使用
    /// - 一次回傳欄位本身設定 + 所屬主檔資訊 + UI 輔助資料
    ///
    /// 此方法不只是查一筆資料，而是：
    /// - 將資料庫中的欄位設定（FORM_FIELD_CONFIG）
    /// - 結合主檔設定（FORM_FIELD_MASTER）
    /// - 再補上 Schema 推導資訊（PK / 可用控制元件 / 查詢元件）
    /// → 組合成前端可直接使用的 ViewModel
    /// </summary>
    /// <param name="fieldId">欄位設定唯一識別碼（FORM_FIELD_CONFIG.ID）</param>
    /// <returns>
    /// 若找到欄位設定，回傳 <see cref="FormFieldViewModel"/>；
    /// 若查無資料或已被軟刪除，回傳 null
    /// </returns>
    public async Task<FormFieldViewModel?> GetFieldById(Guid fieldId)
    {
        // 1) 查詢欄位設定本身（FORM_FIELD_CONFIG）
        var configWhere = new WhereBuilder<FormFieldConfigDto>()
            .AndEq(x => x.ID, fieldId)
            .AndNotDeleted();

        var cfg = await _sqlHelper.SelectFirstOrDefaultAsync(configWhere);

        // 若查無欄位設定，直接回傳 null
        if (cfg == null) return null;

        // 2) 查詢該欄位所屬的表單主檔（FORM_FIELD_MASTER）
        // - 用於判斷 SchemaType（Base / Detail / View / Mapping）
        var masterWhere = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, cfg.FORM_FIELD_MASTER_ID)
            .AndNotDeleted();

        var master = await _sqlHelper.SelectFirstOrDefaultAsync(masterWhere);

        // 3) 從實體資料表 Schema 中取得主鍵欄位清單
        // - 用於判斷此欄位是否為 PK
        // - 前端可據此限制編輯行為（例如 PK 不可修改）
        var pkColumns = _schemaService.GetPrimaryKeyColumns(cfg.TABLE_NAME);

        // 4) 組裝回傳用的 ViewModel
        // - 除了 DB 中的設定值
        // - 同時補齊前端需要的「可選項目」與「推導狀態」
        return new FormFieldViewModel
        {
            // 基本識別資訊
            ID = cfg.ID,
            FORM_FIELD_MASTER_ID = cfg.FORM_FIELD_MASTER_ID,
            TableName = cfg.TABLE_NAME,
            COLUMN_NAME = cfg.COLUMN_NAME,
            DATA_TYPE = cfg.DATA_TYPE,

            // 控制元件設定
            CONTROL_TYPE = cfg.CONTROL_TYPE,

            // 根據資料型別推導允許的控制元件清單（避免前端亂選）
            CONTROL_TYPE_WHITELIST =
                FormFieldHelper.GetControlTypeWhitelist(cfg.DATA_TYPE),

            // 根據資料型別推導允許的查詢元件清單
            QUERY_COMPONENT_TYPE_WHITELIST =
                FormFieldHelper.GetQueryConditionTypeWhitelist(cfg.DATA_TYPE),

            // 欄位行為設定
            IS_REQUIRED = cfg.IS_REQUIRED,
            IS_EDITABLE = cfg.IS_EDITABLE,

            // 判斷此欄位是否有任何驗證規則設定（例如 min / max / regex）
            IS_VALIDATION_RULE = HasValidationRules(cfg.ID),

            // 判斷此欄位是否為資料表主鍵
            IS_PK = pkColumns.Contains(cfg.COLUMN_NAME),

            // 查詢相關設定
            QUERY_DEFAULT_VALUE = cfg.QUERY_DEFAULT_VALUE,
            FIELD_ORDER = cfg.FIELD_ORDER,
            QUERY_COMPONENT = cfg.QUERY_COMPONENT,
            QUERY_CONDITION = cfg.QUERY_CONDITION,
            CAN_QUERY = cfg.CAN_QUERY,

            SchemaType = master.SCHEMA_TYPE
        };
    }

    /// <summary>
    /// 確保指定資料表（或檢視表）的欄位設定已存在。
    ///
    /// 核心用途：
    /// - 當使用者在表單設計器中「首次選擇某資料表 / 檢視表」時
    /// - 若該表尚未建立對應的欄位設定（FORM_FIELD_CONFIG）
    /// - 系統會自動依資料表 schema 建立「預設欄位設定」
    ///
    /// 此方法具備「自我修復（Self-healing）」特性：
    /// - 表結構有新增欄位 → 自動補齊設定
    /// - 舊設定仍存在 → 不覆蓋，只補缺
    ///
    /// 適用情境：
    /// - 表單設計器首次載入某資料表
    /// - 表結構異動後重新進入設計器
    /// - 主檔 / 明細 / 關聯表欄位同步
    /// </summary>
    /// <param name="tableName">資料表或檢視表名稱</param>
    /// <param name="formMasterId">
    /// 所屬的 FORM_FIELD_MASTER Id；
    /// 若為 null，表示此次操作尚未綁定主檔，系統會自動推斷或建立
    /// </param>
    /// <param name="schemaType">
    /// 表結構類型（OnlyTable / OnlyView / OnlyDetail / OnlyMapping）
    /// </param>
    /// <returns>
    /// 包含最新欄位設定的 FormFieldListViewModel；
    /// 若資料表不存在或無欄位，回傳 null
    /// </returns>
    public async Task <FormFieldListViewModel?> EnsureFieldsSaved(string tableName, Guid? formMasterId, TableSchemaQueryType schemaType)
    {
        // 1) 取得資料表 Schema 欄位資訊
        // - 從資料庫實際結構取得欄位名稱與型別
        // - 作為「欄位設定是否齊全」的依據
        var columns = GetTableSchema(tableName);

        // 若查無欄位（例如表不存在），直接回 null
        if (columns.Count == 0) return null;

        // 2) 檢查必要欄位是否存在
        // - 僅針對實體表（Base / Detail / Mapping）進行檢查
        // - View 因為可能是 join 結果，不強制必要欄位
        var missingColumns = _requiredColumns
            .Where(req => !columns.Any(c => c.COLUMN_NAME.Equals(req, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        
        if (missingColumns.Count > 0 &&
            (schemaType == TableSchemaQueryType.OnlyTable ||
             schemaType == TableSchemaQueryType.OnlyDetail ||
             schemaType == TableSchemaQueryType.OnlyMapping))
            throw new Exception(
                $"缺少必要欄位：{string.Join(", ", missingColumns)}");

        // 3) 建立暫時用的 FormFieldMaster 設定
        // - 若後續無法從現有設定推斷 MasterId，會用此物件建立新主檔
        var master = new FormFieldMasterDto
        {
            FORM_NAME = string.Empty,
            STATUS = (int)TableStatusType.Draft,
            SCHEMA_TYPE = schemaType,
            
            // 僅依 schemaType 指定對應的表名，其餘維持 null
            BASE_TABLE_NAME   = null,
            DETAIL_TABLE_NAME = null,
            VIEW_TABLE_NAME   = null,
            MAPPING_TABLE_NAME = null
        };

        switch (schemaType)
        {
            case TableSchemaQueryType.OnlyTable:
                master.BASE_TABLE_NAME = tableName;
                break;
            case TableSchemaQueryType.OnlyView:
                master.VIEW_TABLE_NAME = tableName;
                break;
            case TableSchemaQueryType.OnlyDetail:
                master.DETAIL_TABLE_NAME = tableName;
                break;
            case TableSchemaQueryType.OnlyMapping:
                master.MAPPING_TABLE_NAME = tableName;
                break;
        }
        
        // 決定最終使用的 FORM_FIELD_MASTER_ID：
        // 1. 優先使用呼叫端傳入的 formMasterId
        // 2. 否則從既有欄位設定中推斷
        // 3. 若仍不存在，則建立新的 FormMaster
        // TODO: 之後需要釐清是否需要configs.Values.FirstOrDefault()?.FORM_FIELD_MASTER_ID
        var masterId = formMasterId ?? GetOrCreateFormMasterId(master);
        
        // 4) 取得目前已存在的欄位設定（若有）
        // - formMasterId 代表「這次操作所屬的主檔」
        // - 若為 null，代表可能是第一次進入，需要從現有設定或建立新主檔
        var configs = await GetFieldConfigs(tableName, masterId);
        
        // 5) 計算欄位排序起始值
        // - 既有欄位保留原排序
        // - 新欄位接在最後
        var maxOrder = configs.Values.Any()
            ? configs.Values.Max(x => x.FIELD_ORDER)
            : 0;

        var order = maxOrder;

        // 6) 補齊尚未建立設定的欄位
        // - 僅針對「資料表實際存在，但設定檔中沒有」的欄位
        // - 不覆蓋既有設定，避免破壞使用者自訂內容
        foreach (var col in columns)
        {
            if (!configs.ContainsKey(col.COLUMN_NAME))
            {
                order++;

                var vm = CreateDefaultFieldConfig(
                    col.COLUMN_NAME,
                    col.DATA_TYPE,
                    masterId,
                    tableName,
                    order,
                    schemaType);

                UpsertField(vm, masterId);
            }
        }

        // 7) 重新查詢所有欄位設定，確保回傳資料與 DB 同步
        var result = await GetFieldsByTableName(tableName, masterId, schemaType);

        // 8) 主檔（Base Table）額外處理：
        // - 因為主檔維護功能，更新主檔可能會需要自訂下拉選單
        // - 預設為所有欄位建立 Dropdown 設定
        // - IS_USE_SQL 等設定可為 null，後續由使用者調整
        if (schemaType == TableSchemaQueryType.OnlyTable)
        {
            foreach (var field in result.Fields)
            {
                EnsureDropdownCreated(field.ID, null, null);
            }
        }

        return result;
    }

    /// <summary>
    /// 新增或更新欄位設定，若已存在則更新，否則新增。
    /// </summary>
    /// <param name="model">表單欄位的 ViewModel</param>
    public void UpsertField(FormFieldViewModel model, Guid formMasterId)
    {
        var controlType = model.CONTROL_TYPE ?? FormFieldHelper.GetDefaultControlType(model.DATA_TYPE);

        // 只有在欄位可編輯時才允許設定必填
        var isRequired = model.IS_EDITABLE == true && model.IS_REQUIRED == true;

        var param = new
        {
            ID = model.ID == Guid.Empty ? Guid.NewGuid() : model.ID,
            FORM_FIELD_MASTER_ID = formMasterId,
            TABLE_NAME = model.TableName,
            model.COLUMN_NAME,
            model.DATA_TYPE,
            CONTROL_TYPE = controlType,
            IS_REQUIRED = isRequired,
            model.IS_EDITABLE,
            model.QUERY_DEFAULT_VALUE,
            model.FIELD_ORDER,
            model.QUERY_COMPONENT,
            model.QUERY_CONDITION,
            model.CAN_QUERY
        };

        var affected = _con.Execute(Sql.UpsertField, param);
        
        if (affected == 0)
        {
            throw new InvalidOperationException($"Upsert 失敗：{model.COLUMN_NAME} 無法新增或更新");
        }
    }

    /// <summary>
    /// 批次設定欄位的必填狀態，僅對可編輯欄位生效。
    /// </summary>
    public Guid GetFormFieldMasterChildren(Guid formMasterId)
    {
        Guid children = _con.QueryFirstOrDefault<Guid>(Sql.GetFormFieldMasterChildren, new { formMasterId, SchemaType = TableSchemaQueryType.All.ToInt() });
        return children;
    }
    
    /// <summary>
    /// 批次設定欄位的可編輯狀態。
    /// 若設定為不可編輯，會同步取消必填。
    /// </summary>
    public async Task<string> SetAllEditable( Guid formMasterId, bool isEditable, CancellationToken ct )
    {
        _con.Execute(Sql.SetAllEditable, new { formMasterId, isEditable });
        
        var where = new WhereBuilder<FormFieldConfigDto>()
            .AndEq(x => x.FORM_FIELD_MASTER_ID, formMasterId)
            .AndNotDeleted();
        
        var model = await _sqlHelper.SelectFirstOrDefaultAsync( where, ct );
        return model.TABLE_NAME;
    }

    /// <summary>
    /// 批次設定欄位的必填狀態，僅對可編輯欄位生效。
    /// </summary>
    public async Task<string> SetAllRequired( Guid formMasterId, bool isRequired, CancellationToken ct )
    {
        // Guid children = GetFormFieldMasterChildren(formMasterId);
        _con.Execute(Sql.SetAllRequired, new { formMasterId, isRequired });
        
        var where = new WhereBuilder<FormFieldConfigDto>()
            .AndEq(x => x.FORM_FIELD_MASTER_ID, formMasterId)
            .AndNotDeleted();
        
        var model = await _sqlHelper.SelectFirstOrDefaultAsync( where, ct );
        return model.TABLE_NAME;
    }

    /// <summary>
    /// 檢查指定 FORM_FIELD_CONFIG ID 是否已存在於設定資料表中。
    /// </summary>
    /// <param name="fieldId">欄位唯一識別碼</param>
    /// <returns>若存在則為 true，否則為 false</returns>
    public bool CheckFieldExists(Guid fieldId)
    {
        var res = _con.ExecuteScalar<int>(Sql.CheckFieldExists, new { fieldId }) > 0;
        return res;
    }

    /// <summary>
    /// 取得 FORM_FIELD_VALIDATION_RULE 的所有驗證規則（包含順序與錯誤訊息）。
    /// </summary>
    /// <param name="fieldId">欄位唯一識別碼</param>
    /// <returns>回傳驗證規則清單</returns>
    /// <summary>
    /// 依欄位設定 ID 取回該欄位的所有驗證規則（過濾已刪除），並依 SEQNO/ID 排序。
    /// </summary>
    public async Task<List<FormFieldValidationRuleDto>> GetValidationRulesByFieldId( Guid fieldId, CancellationToken ct = default )
    {
        var where = new WhereBuilder<FormFieldValidationRuleDto>()
            .AndEq(x => x.FIELD_CONFIG_ID, fieldId)
            .AndNotDeleted();
        
        var rules = await _sqlHelper.SelectWhereAsync( where, ct );
        return rules;
    }

    /// <summary>
    /// 判斷欄位是否已設定任何驗證規則。
    /// </summary>
    /// <param name="fieldId">欄位唯一識別碼</param>
    /// <returns>若有規則則回傳 true</returns>
    public bool HasValidationRules(Guid fieldId)
    {
        var res = _con.ExecuteScalar<int>(Sql.CountValidationRules, new { fieldId }) > 0;
        return res;
    }

    /// <summary>
    /// 新增空的驗證規則
    /// </summary>
    /// <param name="fieldConfigId"></param>
    /// <returns></returns>
    public FormFieldValidationRuleDto CreateEmptyValidationRule(Guid fieldConfigId)
    {
        return new FormFieldValidationRuleDto
        {
            ID = Guid.NewGuid(),
            FIELD_CONFIG_ID = fieldConfigId,
            VALIDATION_VALUE = "",
            MESSAGE_ZH = "",
            MESSAGE_EN = "",
            VALIDATION_ORDER = GetNextValidationOrder(fieldConfigId)
        };
    }

    /// <summary>
    /// 新增一筆欄位驗證規則。
    /// </summary>
    /// <param name="model"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> InsertValidationRule( FormFieldValidationRuleDto model, CancellationToken ct = default )
    {

        var count = await _sqlHelper.InsertAsync( model, ct );
        return count > 0;
    }

    /// <summary>
    /// 取得該欄位的下一個驗證順序編號（遞增）。
    /// </summary>
    /// <param name="fieldId">欄位唯一識別碼</param>
    /// <returns>回傳下一個排序值</returns>
    public int GetNextValidationOrder(Guid fieldId)
    {
        var res = _con.ExecuteScalar<int>(Sql.GetNextValidationOrder, new { fieldId });
        return res;
    }
    
    /// <summary>
    /// 根據欄位 ID 取得該欄位的控制類型（FormControlType Enum）。
    /// </summary>
    /// <param name="fieldId">欄位唯一識別碼</param>
    /// <returns>回傳控制類型 Enum</returns>
    public FormControlType GetControlTypeByFieldId(Guid fieldId)
    {
        var value = _con.ExecuteScalar<int?>(Sql.GetControlTypeByFieldId, new { fieldId }) ?? 0;
        return (FormControlType)value;
    }

    /// <summary>
    /// 儲存（更新）驗證規則
    /// </summary>
    /// <param name="model"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> SaveValidationRule( FormFieldValidationRuleDto model, CancellationToken ct = default )
    { 
        var res = await _sqlHelper.UpdateAllByIdAsync(model, UpdateNullBehavior.IgnoreNulls, true, ct)  > 0;
        return res;
    }

    /// <summary>
    /// 刪除一筆驗證規則。
    /// </summary>
    /// <param name="id">驗證規則的唯一識別碼</param>
    /// <returns>刪除成功則回傳 true</returns>
    public async Task<bool> DeleteValidationRule( Guid id , CancellationToken ct = default )
    {
        var where = new WhereBuilder<FormFieldValidationRuleDto>()
            .AndEq(x => x.ID, id);
        var res = await _sqlHelper.DeleteWhereAsync( where, ct ) > 0;
        return res;
    }

    /// <summary>
    /// 確保 FORM_FIELD_DROPDOWN 存在，
    /// 可依需求指定預設的 SQL 來源與是否使用 SQL。
    /// </summary>
    /// <param name="fieldId">欄位設定 ID</param>
    /// <param name="isUseSql">是否使用 SQL 為資料來源，預設為 false；OnlyView 可帶入 null</param>
    /// <param name="sql">預設 SQL 查詢語句，預設為 null</param>
    public void EnsureDropdownCreated(Guid fieldId, bool? isUseSql = false, string? sql = null)
    {
        _con.Execute(Sql.EnsureDropdownExists, new { fieldId, isUseSql, sql });
    }
    
    public async Task<DropDownViewModel> GetDropdownSetting( Guid fieldId, CancellationToken ct = default )
    {
        var model = new DropDownViewModel();
        var where = new WhereBuilder<FormDropDownDto>()
            .AndEq(x => x.FORM_FIELD_CONFIG_ID, fieldId)
            .AndNotDeleted();
        
        var dropDown = await _sqlHelper.SelectFirstOrDefaultAsync( where, ct );
        if(dropDown == null) throw new Exception("查無下拉選單設定，且確認傳入的id是否正確");
        
        model.FormDropDown = dropDown;
        var optionTexts = GetDropdownOptions( dropDown.ID, ct );
        model.OPTION_TEXT = optionTexts;

        return model;
    }
    
    public async Task<List<FormFieldDropdownOptionsDto>> GetDropdownOptions( Guid dropDownId, CancellationToken ct = default )
    {
        var dropdownWhere = new WhereBuilder<FormDropDownDto>()
            .AndEq(x => x.ID, dropDownId)
            .AndNotDeleted();

        var dropdown = await _sqlHelper.SelectFirstOrDefaultAsync( dropdownWhere, ct );
        if ( dropdown is null )
            return new();

        if ( !dropdown.ISUSESQL )
        {
            var optionWhere = new WhereBuilder<FormFieldDropdownOptionsDto>()
                .AndEq(x => x.FORM_FIELD_DROPDOWN_ID, dropDownId)
                .AndNotDeleted();

            return await _sqlHelper.SelectWhereAsync( optionWhere, ct );
        }

        if ( string.IsNullOrWhiteSpace( dropdown.DROPDOWNSQL ) )
            return new();

        try
        {
            var syncResult = _dropdownSqlSyncService.Sync( dropDownId, dropdown.DROPDOWNSQL );
            return syncResult.Options;
        }
        catch ( DropdownSqlSyncException ex )
        {
            throw new InvalidOperationException( $"同步下拉選項失敗：{ex.Message}", ex );
        }
    }

    public Guid SaveDropdownOption(Guid? id, Guid dropdownId, string optionText, string optionValue, string? optionTable = null)
    {
        var param = new
        {
            Id = (id == Guid.Empty ? null : id),
            DropdownId = dropdownId,
            OptionText = optionText,
            OptionValue = optionValue,
            OptionTable = optionTable
        };

        // ExecuteScalar 直接拿回 OUTPUT 的 Guid
        return _con.ExecuteScalar<Guid>(Sql.UpsertDropdownOption, param);
    }

    public async Task<bool> DeleteDropdownOption( Guid optionId, CancellationToken ct = default )
    {
        var where = new WhereBuilder<FormFieldDropdownOptionsDto>()
            .AndEq(x => x.ID, optionId);
        return await _sqlHelper.DeleteWhereAsync(where, ct) > 0;
    }
    
    public Task SaveDropdownSql( Guid dropdownId, string sql, CancellationToken ct )
    {
        return _sqlHelper.UpdateById<FormDropDownDto>(dropdownId)
            .Set(x => x.ISUSESQL, true)
            .Set(x => x.DROPDOWNSQL, sql)
            .ExecuteAsync(ct);
    }
    
    public Task SetDropdownMode( Guid dropdownId, bool isUseSql, CancellationToken ct )
    {
        return _sqlHelper.UpdateById<FormDropDownDto>(dropdownId)
            .Set(x => x.ISUSESQL, isUseSql)
            .ExecuteAsync(ct);
    }
    
    public ValidateSqlResultViewModel ValidateDropdownSql( string sql )
    {
        var result = new ValidateSqlResultViewModel();

        try
        {
            if ( string.IsNullOrWhiteSpace( sql ) )
            {
                result.Success = false;
                result.Message = "SQL 不可為空。";
                return result;
            }

            if ( Regex.IsMatch(sql, 
                    @"\b(insert|update|delete|drop|alter|truncate|exec|merge)\b", RegexOptions.IgnoreCase ) )
            {
                result.Success = false;
                result.Message = "僅允許查詢類 SQL。";
                return result;
            }

            var wasClosed = _con.State != System.Data.ConnectionState.Open;
            if ( wasClosed ) _con.Open();

            using var cmd = new SqlCommand( sql, _con );
            using var reader = cmd.ExecuteReader();

            var columns = reader.GetColumnSchema();
            if ( columns.Count < 2 )
            {
                result.Success = false;
                result.Message = "SQL 必須回傳至少兩個欄位，SELECT A AS ID, B AS NAME";
                return result;
            }

            // 檢查第一個欄位是否包含任一個 _excludeColumns 關鍵字
            if ( !_excludeColumns.Any(ex =>
                    columns[0].ColumnName.Contains( ex, StringComparison.OrdinalIgnoreCase) ) )
            {
                result.Success = false;
                result.Message = $"第一個欄位必須包含任一關鍵字：{ string.Join (", ", _excludeColumns ) }";
                return result;
            }

            var rows = new List<Dictionary<string, object>>();
            while ( reader.Read() )
            {
                var row = new Dictionary<string, object>();
                for ( int i = 0; i < reader.FieldCount; i++ )
                {
                    row[columns[i].ColumnName] = reader.GetValue( i );
                }
                rows.Add(row);
            }

            result.Success = true;
            result.RowCount = rows.Count;
            result.Rows = rows.Take(10).ToList(); // 最多回傳前 10 筆

            if ( wasClosed ) _con.Close();
        }
        catch ( Exception ex )
        {
            result.Success = false;
            result.Message = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// 從 SQL 匯入下拉選項：要求結果欄位別名為 ID 與 NAME
    /// 1) 先 Validate SQL
    /// 2) 解析來源表名（optionTable）
    /// 3) 使用交易確保全有/全無
    /// 4) 強型別讀取，明確檢查 NULL/空字串，錯誤時回傳第 N 筆
    /// 5) 安全的參數化寫入（忽略重複）
    /// </summary>
    public ValidateSqlResultViewModel ImportDropdownOptionsFromSql(string sql, Guid dropdownId)
    {
        var validation = ValidateDropdownSql( sql );
        if ( !validation.Success )
            return validation;

        var wasClosed = _con.State != System.Data.ConnectionState.Open;
        if ( wasClosed )
            _con.Open();

        using var tx = _con.BeginTransaction();
        try
        {
            _con.Execute( Sql.UpdateDropdownSql, new { DropdownId = dropdownId, Sql = sql }, tx );

            var syncResult = _dropdownSqlSyncService.Sync( dropdownId, sql, tx );

            tx.Commit();

            return new ValidateSqlResultViewModel
            {
                Success = true,
                Message = "匯入完成。",
                RowCount = syncResult.RowCount,
                Rows = syncResult.PreviewRows
            };
        }
        catch ( DropdownSqlSyncException ex )
        {
            tx.Rollback();
            return new ValidateSqlResultViewModel
            {
                Success = false,
                Message = ex.Message
            };
        }
        catch ( Exception ex )
        {
            tx.Rollback();
            return new ValidateSqlResultViewModel
            {
                Success = false,
                Message = $"匯入失敗：{ex.Message}"
            };
        }
        finally
        {
            if ( wasClosed )
                _con.Close();
        }
    }
    
    public async Task<Guid> SaveFormHeader( FormHeaderViewModel model )
    {
        var whereBase = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, model.BASE_TABLE_ID)
            .AndNotDeleted();

        var whereView = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, model.VIEW_TABLE_ID)
            .AndNotDeleted();
        
        var baseMaster = await _sqlHelper.SelectFirstOrDefaultAsync(whereBase)
                         ?? throw new InvalidOperationException("主表查無資料");
        var viewMaster = await _sqlHelper.SelectFirstOrDefaultAsync(whereView)
                         ?? throw new InvalidOperationException("檢視表查無資料");

        var baseTableName = baseMaster.BASE_TABLE_NAME;
        var viewTableName = viewMaster.VIEW_TABLE_NAME;
        
        // 確保主表與顯示用 View 皆能成功查詢，避免儲存無效設定
        if ( GetTableSchema(baseTableName!).Count == 0 )
            throw new InvalidOperationException("主表名稱查無資料");

        if ( GetTableSchema(viewTableName!).Count == 0)
            throw new InvalidOperationException("顯示用 View 名稱查無資料");

        // 若未指定 ID 則產生新 ID
        if (model.ID == Guid.Empty)
        {
            model.ID = Guid.NewGuid();
        }

        var id = _con.ExecuteScalar<Guid>(Sql.UpsertFormMaster, new
        {
            model.ID,
            model.FORM_NAME,
            model.BASE_TABLE_ID,
            model.VIEW_TABLE_ID,
            BASE_TABLE_NAME = baseTableName,
            VIEW_TABLE_NAME = viewTableName,
            MAPPING_TABLE_NAME = (string?)null,
            MAPPING_TABLE_ID = (Guid?)null,
            STATUS = (int)TableStatusType.Active,
            SCHEMA_TYPE = TableSchemaQueryType.All,
            FUNCTION_TYPE = FormFunctionType.MasterMaintenance
        });
        return id;
    }

    public async Task<Guid> SaveMasterDetailFormHeader(MasterDetailFormHeaderViewModel model)
    {
        var whereBase = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, model.BASE_TABLE_ID)
            .AndNotDeleted();
        
        var whereDetail = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, model.DETAIL_TABLE_ID)
            .AndNotDeleted();

        var whereView = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, model.VIEW_TABLE_ID)
            .AndNotDeleted();
        
        var baseMaster = await _sqlHelper.SelectFirstOrDefaultAsync(whereBase)
                         ?? throw new InvalidOperationException("主表查無資料");
        var detailMaster = await _sqlHelper.SelectFirstOrDefaultAsync(whereDetail)
                         ?? throw new InvalidOperationException("明細表無資料");
        var viewMaster = await _sqlHelper.SelectFirstOrDefaultAsync(whereView)
                         ?? throw new InvalidOperationException("檢視表查無資料");
        
        var masterTableName = baseMaster.BASE_TABLE_NAME;
        var detailTableName = detailMaster.DETAIL_TABLE_NAME;
        var viewTableName = viewMaster.VIEW_TABLE_NAME;

        if (GetTableSchema(masterTableName!).Count == 0)
            throw new InvalidOperationException("主表名稱查無資料");

        if (GetTableSchema(detailTableName!).Count == 0)
            throw new InvalidOperationException("明細表名稱查無資料");

        if (GetTableSchema(viewTableName!).Count == 0)
            throw new InvalidOperationException("顯示用 View 名稱查無資料");

        // 確保主表與明細表具有共用的關聯欄位，避免 SubmitForm 發生錯誤
        EnsureRelationColumn(masterTableName!, detailTableName!, "主表與明細表");

        if (model.ID == Guid.Empty)
        {
            model.ID = Guid.NewGuid();
        }

        var id = _con.ExecuteScalar<Guid>(Sql.UpsertMasterDetailFormMaster, new
        {
            model.ID,
            model.FORM_NAME,
            model.BASE_TABLE_ID,
            model.DETAIL_TABLE_ID,
            model.VIEW_TABLE_ID,
            MASTER_TABLE_NAME = masterTableName,
            DETAIL_TABLE_NAME = detailTableName,
            VIEW_TABLE_NAME = viewTableName,
            MAPPING_TABLE_NAME = (string?)null,
            MAPPING_TABLE_ID = (Guid?)null,
            STATUS = (int)TableStatusType.Active,
            SCHEMA_TYPE = TableSchemaQueryType.All,
            FUNCTION_TYPE = FormFunctionType.MasterDetailMaintenance
        });
        return id;
    }

    /// <summary>
    /// 儲存多對多表單主檔設定並回寫 FORM_FIELD_MASTER。
    /// </summary>
    /// <param name="model">多對多主檔設定</param>
    /// <remarks>會同步檢查主表、目標表與關聯表的實體存在性與關聯欄位，以避免後續填寫階段出錯。</remarks>
    public async Task<Guid> SaveMultipleMappingFormHeader(MultipleMappingFormHeaderViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.MAPPING_BASE_FK_COLUMN) ||
            string.IsNullOrWhiteSpace(model.MAPPING_DETAIL_FK_COLUMN))
        {
            throw new InvalidOperationException("必須設定對應的關聯欄位名稱");
        }

        ValidateColumnName(model.MAPPING_BASE_FK_COLUMN);
        ValidateColumnName(model.MAPPING_DETAIL_FK_COLUMN);

        var whereBase = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, model.BASE_TABLE_ID)
            .AndNotDeleted();
        var whereDetail = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, model.DETAIL_TABLE_ID)
            .AndNotDeleted();
        var whereMapping = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, model.MAPPING_TABLE_ID)
            .AndNotDeleted();

        var viewTableId = model.VIEW_TABLE_ID == Guid.Empty ? null : model.VIEW_TABLE_ID;
        var whereView = viewTableId.HasValue
            ? new WhereBuilder<FormFieldMasterDto>().AndEq(x => x.ID, viewTableId).AndNotDeleted()
            : null;

        var baseMaster = await _sqlHelper.SelectFirstOrDefaultAsync(whereBase)
                         ?? throw new InvalidOperationException("主表查無資料");
        var detailMaster = await _sqlHelper.SelectFirstOrDefaultAsync(whereDetail)
                           ?? throw new InvalidOperationException("目標表查無資料");
        var mappingMaster = await _sqlHelper.SelectFirstOrDefaultAsync(whereMapping)
                            ?? throw new InvalidOperationException("關聯表查無資料");
        var viewMaster = whereView == null
            ? null
            : await _sqlHelper.SelectFirstOrDefaultAsync(whereView)
              ?? throw new InvalidOperationException("檢視表查無資料");

        var baseTableName = baseMaster.BASE_TABLE_NAME;
        var detailTableName = detailMaster.DETAIL_TABLE_NAME;
        var mappingTableName = mappingMaster.MAPPING_TABLE_NAME;
        var viewTableName = viewMaster?.VIEW_TABLE_NAME;

        if (string.IsNullOrWhiteSpace(baseTableName))
            throw new InvalidOperationException("主表名稱查無設定");
        if (string.IsNullOrWhiteSpace(detailTableName))
            throw new InvalidOperationException("目標表名稱查無設定");
        if (string.IsNullOrWhiteSpace(mappingTableName))
            throw new InvalidOperationException("關聯表名稱查無設定");

        if (GetTableSchema(baseTableName).Count == 0)
            throw new InvalidOperationException("主表名稱查無資料");
        if (GetTableSchema(detailTableName).Count == 0)
            throw new InvalidOperationException("目標表名稱查無資料");
        if (GetTableSchema(mappingTableName).Count == 0)
            throw new InvalidOperationException("關聯表名稱查無資料");
        if (!string.IsNullOrWhiteSpace(viewTableName) && GetTableSchema(viewTableName).Count == 0)
            throw new InvalidOperationException("檢視表名稱查無資料");

        EnsureColumnExists(mappingTableName, model.MAPPING_BASE_FK_COLUMN, "關聯表缺少指向主表的外鍵欄位");
        EnsureColumnExists(mappingTableName, model.MAPPING_DETAIL_FK_COLUMN, "關聯表缺少指向明細表的外鍵欄位");
        EnsureColumnExists(baseTableName, model.MAPPING_BASE_FK_COLUMN, "主表缺少對應的主鍵欄位");
        EnsureColumnExists(detailTableName, model.MAPPING_DETAIL_FK_COLUMN, "目標表缺少對應的主鍵欄位");

        if (model.ID == Guid.Empty)
        {
            model.ID = Guid.NewGuid();
        }

        var id = _con.ExecuteScalar<Guid>(Sql.UpsertMultipleMappingFormMaster, new
        {
            model.ID,
            model.FORM_NAME,
            model.BASE_TABLE_ID,
            model.DETAIL_TABLE_ID,
            model.MAPPING_TABLE_ID,
            VIEW_TABLE_ID = viewTableId,
            model.MAPPING_BASE_FK_COLUMN,
            model.MAPPING_DETAIL_FK_COLUMN,
            MASTER_TABLE_NAME = baseTableName,
            DETAIL_TABLE_NAME = detailTableName,
            MAPPING_TABLE_NAME = mappingTableName,
            VIEW_TABLE_NAME = viewTableName,
            STATUS = (int)TableStatusType.Active,
            SCHEMA_TYPE = TableSchemaQueryType.All,
            FUNCTION_TYPE = FormFunctionType.MultipleMappingMaintenance
        });

        return id;
    }
    
    public async Task<bool> CheckMasterDetailFormMasterExistsAsync(
        Guid masterTableId, 
        Guid detailTableId, 
        Guid viewTableId, 
        Guid? excludeId = null)
    {
        // ExecuteScalarAsync 本身就是 async，不會阻塞 ThreadPool
        var count = await _con.ExecuteScalarAsync<int>(
            Sql.CheckMasterDetailFormMasterExists,
            new { masterTableId, detailTableId, viewTableId, excludeId }
        );

        return count > 0;
    }

    public bool CheckFormMasterExists(Guid baseTableId, Guid viewTableId, Guid? excludeId = null)
    {
        var count = _con.ExecuteScalar<int>(Sql.CheckFormMasterExists,
            new { baseTableId, viewTableId, excludeId });
        return count > 0;
    }
    
    #endregion

    #region Private Helpers

    /// <summary>
    /// 從 SQL Server 的 INFORMATION_SCHEMA 取得指定表的欄位結構。
    /// </summary>
    /// <param name="tableName">資料表名稱</param>
    /// <returns>回傳欄位定義清單</returns>
    private List<DbColumnInfo> GetTableSchema(string tableName)
    {
        var sql = Sql.TableSchemaSelect;
        var columns = _con.Query<DbColumnInfo>(sql, new { TableName = tableName }).ToList();
        
        return columns;
    }

    /// <summary>
    /// 從 FORM_FIELD_CONFIG 查出該表的欄位設定資訊，並組成 Dictionary。
    /// </summary>
    /// <param name="tableName">資料表名稱</param>
    /// <returns>回傳以 COLUMN_NAME 為鍵的設定資料</returns>
    private async Task<Dictionary<string, FormFieldConfigDto>> GetFieldConfigs(string tableName, Guid? formMasterId)
    {
        var where = new WhereBuilder<FormFieldConfigDto>()
            .AndEq(x => x.TABLE_NAME, tableName)
            .AndNotDeleted();

        // 避免 formMasterId == null 產生 "FORM_FIELD_MASTER_ID = NULL" 導致查不到資料
        if (formMasterId.HasValue)
        {
            where.AndEq(x => x.FORM_FIELD_MASTER_ID, formMasterId.Value);
        }
        
        var res = await _sqlHelper.SelectWhereAsync( where );
        return res.ToDictionary(x => x.COLUMN_NAME, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 驗證指定資料表是否存在共用的關聯欄位。
    /// </summary>
    /// <param name="leftTableName">左側資料表名稱</param>
    /// <param name="rightTableName">右側資料表名稱</param>
    /// <param name="relationDescription">業務描述，便於錯誤訊息辨識</param>
    /// <exception cref="InvalidOperationException">當找不到符合條件的欄位時拋出</exception>
    private void EnsureRelationColumn(string leftTableName, string rightTableName, string relationDescription)
    {
        var masterCols = _schemaService.GetFormFieldMaster(leftTableName);
        var detailSet = _schemaService
            .GetFormFieldMaster(rightTableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasRelation = masterCols.Any(columnName =>
            detailSet.Contains(columnName) &&
            _relationColumnSuffixes.MatchesRelationSuffix(columnName));

        if (!hasRelation)
        {
            var suffixDisplay = string.Join("', '", _relationColumnSuffixes);
            throw new InvalidOperationException(
                $"{relationDescription}缺少以以下任一結尾的共用關聯欄位：'{suffixDisplay}'");
        }
    }

    /// <summary>
    /// 驗證欄位名稱僅包含英數與底線，避免 SQL Injection 與錯誤設定。
    /// </summary>
    private static void ValidateColumnName(string columnName)
    {
        if (!Regex.IsMatch(columnName, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"欄位名稱僅允許英數與底線：{columnName}");
        }
    }

    /// <summary>
    /// 確認指定資料表存在目標欄位。
    /// </summary>
    private void EnsureColumnExists(string tableName, string columnName, string errorMessage)
    {
        var columns = _schemaService.GetFormFieldMaster(tableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columns.Contains(columnName))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }


    /// <summary>
    /// 查詢所有有設定驗證規則的欄位 ID 清單。
    /// </summary>
    /// <returns>回傳欄位 ID 的 HashSet</returns>
    private HashSet<Guid> GetRequiredFieldIds()
    {
        var res = _con.Query<Guid>(Sql.GetRequiredFieldIds).ToHashSet();
        return res;
    }
    
    private FormFieldViewModel CreateDefaultFieldConfig(string columnName, string dataType, Guid masterId, string tableName, int index, TableSchemaQueryType schemaType)
    {
        return new FormFieldViewModel
        {
            ID = Guid.NewGuid(),
            FORM_FIELD_MASTER_ID = masterId,
            TableName = tableName,
            COLUMN_NAME = columnName,
            DATA_TYPE = dataType,
            CONTROL_TYPE = FormFieldHelper.GetDefaultControlType(dataType), // 依型態決定 ControlType
            IS_REQUIRED = false,
            IS_EDITABLE = true,
            FIELD_ORDER = index,
            QUERY_DEFAULT_VALUE = null,
            SchemaType = schemaType,
            QUERY_COMPONENT = QueryComponentType.None,
            QUERY_CONDITION = ConditionType.None,
            // QUERY_CONDITION_SQL = string.Empty,
            CAN_QUERY = false
        };
    }

    #endregion

    #region SQL
    private static class Sql
    {
        public const string GetFormFieldMasterChildren = @"/**/
SELECT BASE_TABLE_ID 
FROM FORM_FIELD_MASTER 
WHERE id = @formMasterId
AND SCHEMA_TYPE = @SchemaType";

        // 20250814，主檔可以和主檔本身自我關聯
        public const string TableSchemaSelect = @"/**/
SELECT COLUMN_NAME, DATA_TYPE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName
ORDER BY ORDINAL_POSITION";

        public const string UpsertFormMaster = @"/**/
MERGE FORM_FIELD_MASTER AS target
USING (SELECT @ID AS ID) AS src
ON target.ID = src.ID
WHEN MATCHED THEN
    UPDATE SET
        FORM_NAME          = @FORM_NAME,
        BASE_TABLE_NAME    = @BASE_TABLE_NAME,
        VIEW_TABLE_NAME    = @VIEW_TABLE_NAME,
        MAPPING_TABLE_NAME = @MAPPING_TABLE_NAME,
        BASE_TABLE_ID      = @BASE_TABLE_ID,
        VIEW_TABLE_ID      = @VIEW_TABLE_ID,
        MAPPING_TABLE_ID   = @MAPPING_TABLE_ID,
        STATUS             = @STATUS,          
        SCHEMA_TYPE        = @SCHEMA_TYPE,    
        FUNCTION_TYPE      = @FUNCTION_TYPE
WHEN NOT MATCHED THEN
    INSERT (
        ID, FORM_NAME, BASE_TABLE_NAME, VIEW_TABLE_NAME, MAPPING_TABLE_NAME,
        BASE_TABLE_ID, VIEW_TABLE_ID, MAPPING_TABLE_ID, STATUS, SCHEMA_TYPE, FUNCTION_TYPE, IS_DELETE)
    VALUES (
        @ID, @FORM_NAME, @BASE_TABLE_NAME, @VIEW_TABLE_NAME, @MAPPING_TABLE_NAME,
        @BASE_TABLE_ID, @VIEW_TABLE_ID, @MAPPING_TABLE_ID, @STATUS, @SCHEMA_TYPE, @FUNCTION_TYPE, 0)
OUTPUT INSERTED.ID;";

        public const string CheckFormMasterExists = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_MASTER
WHERE BASE_TABLE_ID = @baseTableId
  AND VIEW_TABLE_ID = @viewTableId
  AND (@excludeId IS NULL OR ID <> @excludeId)";

        public const string UpsertMasterDetailFormMaster = @"/**/
MERGE FORM_FIELD_MASTER AS target
USING (SELECT @ID AS ID) AS src
ON target.ID = src.ID
WHEN MATCHED THEN
    UPDATE SET
        FORM_NAME         = @FORM_NAME,
        BASE_TABLE_NAME   = @MASTER_TABLE_NAME,
        DETAIL_TABLE_NAME = @DETAIL_TABLE_NAME,
        VIEW_TABLE_NAME   = @VIEW_TABLE_NAME,
        BASE_TABLE_ID     = @BASE_TABLE_ID,
        DETAIL_TABLE_ID   = @DETAIL_TABLE_ID,
        VIEW_TABLE_ID     = @VIEW_TABLE_ID,
        STATUS            = @STATUS,          
        SCHEMA_TYPE       = @SCHEMA_TYPE,    
        MAPPING_TABLE_NAME = @MAPPING_TABLE_NAME,
        MAPPING_TABLE_ID   = @MAPPING_TABLE_ID,
        FUNCTION_TYPE      = @FUNCTION_TYPE
WHEN NOT MATCHED THEN
    INSERT (
        ID, FORM_NAME, BASE_TABLE_NAME, DETAIL_TABLE_NAME, VIEW_TABLE_NAME, MAPPING_TABLE_NAME,
        BASE_TABLE_ID, DETAIL_TABLE_ID, VIEW_TABLE_ID, MAPPING_TABLE_ID, STATUS, SCHEMA_TYPE, FUNCTION_TYPE, IS_DELETE)
    VALUES (
        @ID, @FORM_NAME, @MASTER_TABLE_NAME, @DETAIL_TABLE_NAME, @VIEW_TABLE_NAME, @MAPPING_TABLE_NAME,
        @BASE_TABLE_ID, @DETAIL_TABLE_ID, @VIEW_TABLE_ID, @MAPPING_TABLE_ID, @STATUS, @SCHEMA_TYPE, @FUNCTION_TYPE, 0)
OUTPUT INSERTED.ID;";

        public const string CheckMasterDetailFormMasterExists = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_MASTER
WHERE BASE_TABLE_ID = @masterTableId
  AND DETAIL_TABLE_ID = @detailTableId
  AND VIEW_TABLE_ID = @viewTableId
  AND (@excludeId IS NULL OR ID <> @excludeId)";

        public const string UpsertMultipleMappingFormMaster = @"/**/
MERGE FORM_FIELD_MASTER AS target
USING (SELECT @ID AS ID) AS src
ON target.ID = src.ID
WHEN MATCHED THEN
    UPDATE SET
        FORM_NAME          = @FORM_NAME,
        BASE_TABLE_NAME    = @MASTER_TABLE_NAME,
        DETAIL_TABLE_NAME  = @DETAIL_TABLE_NAME,
        MAPPING_TABLE_NAME = @MAPPING_TABLE_NAME,
        VIEW_TABLE_NAME    = @VIEW_TABLE_NAME,
        BASE_TABLE_ID      = @BASE_TABLE_ID,
        DETAIL_TABLE_ID    = @DETAIL_TABLE_ID,
        MAPPING_TABLE_ID   = @MAPPING_TABLE_ID,
        VIEW_TABLE_ID      = @VIEW_TABLE_ID,
        MAPPING_BASE_FK_COLUMN = @MAPPING_BASE_FK_COLUMN,
        MAPPING_DETAIL_FK_COLUMN = @MAPPING_DETAIL_FK_COLUMN,
        STATUS             = @STATUS,          
        SCHEMA_TYPE        = @SCHEMA_TYPE,    
        FUNCTION_TYPE      = @FUNCTION_TYPE
WHEN NOT MATCHED THEN
    INSERT (
        ID, FORM_NAME, BASE_TABLE_NAME, DETAIL_TABLE_NAME, MAPPING_TABLE_NAME, VIEW_TABLE_NAME,
        BASE_TABLE_ID, DETAIL_TABLE_ID, MAPPING_TABLE_ID, VIEW_TABLE_ID, STATUS, SCHEMA_TYPE, FUNCTION_TYPE, IS_DELETE,
        MAPPING_BASE_FK_COLUMN, MAPPING_DETAIL_FK_COLUMN)
    VALUES (
        @ID, @FORM_NAME, @MASTER_TABLE_NAME, @DETAIL_TABLE_NAME, @MAPPING_TABLE_NAME, @VIEW_TABLE_NAME,
        @BASE_TABLE_ID, @DETAIL_TABLE_ID, @MAPPING_TABLE_ID, @VIEW_TABLE_ID, @STATUS, @SCHEMA_TYPE, @FUNCTION_TYPE, 0,
        @MAPPING_BASE_FK_COLUMN, @MAPPING_DETAIL_FK_COLUMN)
OUTPUT INSERTED.ID;";
        
        public const string UpsertField = @"
MERGE dbo.FORM_FIELD_CONFIG WITH (HOLDLOCK) AS target

USING (
    SELECT
        @FORM_FIELD_MASTER_ID AS FORM_FIELD_MASTER_ID,
        @TABLE_NAME           AS TABLE_NAME,
        @COLUMN_NAME          AS COLUMN_NAME
) AS src
ON  target.FORM_FIELD_MASTER_ID = src.FORM_FIELD_MASTER_ID
AND target.TABLE_NAME           = src.TABLE_NAME
AND target.COLUMN_NAME          = src.COLUMN_NAME
AND target.IS_DELETE            = 0

WHEN MATCHED THEN
    UPDATE SET
        CONTROL_TYPE   = @CONTROL_TYPE,
        IS_REQUIRED     = @IS_REQUIRED,
        IS_EDITABLE    = @IS_EDITABLE,
        QUERY_DEFAULT_VALUE  = @QUERY_DEFAULT_VALUE,
        FIELD_ORDER    = @FIELD_ORDER,
        QUERY_COMPONENT = @QUERY_COMPONENT,
        QUERY_CONDITION = @QUERY_CONDITION,
        CAN_QUERY      = @CAN_QUERY,
        EDIT_TIME      = GETDATE()
WHEN NOT MATCHED THEN
    INSERT (
        ID, FORM_FIELD_MASTER_ID, TABLE_NAME, COLUMN_NAME, DATA_TYPE,
        CONTROL_TYPE, IS_REQUIRED, IS_EDITABLE, QUERY_DEFAULT_VALUE, FIELD_ORDER, QUERY_COMPONENT, QUERY_CONDITION, CAN_QUERY, CREATE_TIME, IS_DELETE
    )
    VALUES (
        @ID, @FORM_FIELD_MASTER_ID, @TABLE_NAME, @COLUMN_NAME, @DATA_TYPE,
        @CONTROL_TYPE, @IS_REQUIRED, @IS_EDITABLE, @QUERY_DEFAULT_VALUE, @FIELD_ORDER, @QUERY_COMPONENT, @QUERY_CONDITION, @CAN_QUERY, GETDATE(), 0
    );";

        public const string CheckFieldExists         = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_CONFIG WHERE ID = @fieldId";

        public const string SetAllEditable = @"/**/
UPDATE FORM_FIELD_CONFIG
SET IS_EDITABLE = @isEditable
WHERE FORM_FIELD_MASTER_ID = @formMasterId;

-- 若不可編輯，強制取消必填
IF (@isEditable = 0)
BEGIN
    UPDATE FORM_FIELD_CONFIG
    SET IS_REQUIRED = 0
    WHERE FORM_FIELD_MASTER_ID = @formMasterId;
END
";

        public const string SetAllRequired = @"/**/
UPDATE FORM_FIELD_CONFIG
SET IS_REQUIRED = CASE WHEN @isRequired = 1 AND IS_EDITABLE = 1 THEN 1 ELSE 0 END
WHERE FORM_FIELD_MASTER_ID = @formMasterId";
        
        public const string CountValidationRules     = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_VALIDATION_RULE WHERE FIELD_CONFIG_ID = @fieldId AND IS_DELETE = 0";

        public const string GetNextValidationOrder   = @"/**/
SELECT ISNULL(MAX(VALIDATION_ORDER), 0) + 1 FROM FORM_FIELD_VALIDATION_RULE WHERE FIELD_CONFIG_ID = @fieldId";
        
        public const string GetControlTypeByFieldId  = @"/**/
SELECT CONTROL_TYPE FROM FORM_FIELD_CONFIG WHERE ID = @fieldId";

        public const string GetRequiredFieldIds      = @"/**/
SELECT FIELD_CONFIG_ID FROM FORM_FIELD_VALIDATION_RULE WHERE IS_DELETE = 0";

        public const string EnsureDropdownExists = @"
/* 僅在尚未存在時插入 dropdown 主檔 */
IF NOT EXISTS (
    SELECT 1 FROM FORM_FIELD_DROPDOWN WHERE FORM_FIELD_CONFIG_ID = @fieldId
)
BEGIN
    INSERT INTO FORM_FIELD_DROPDOWN (ID, FORM_FIELD_CONFIG_ID, ISUSESQL, DROPDOWNSQL, IS_DELETE)
    VALUES (NEWID(), @fieldId, @isUseSql, @sql, 0)
END
";

        public const string UpdateDropdownSql = @"/**/
UPDATE FORM_FIELD_DROPDOWN
SET DROPDOWNSQL = @Sql,
    ISUSESQL = 1
WHERE ID = @DropdownId;";
        
        public const string UpsertDropdownOption = @"/**/
MERGE dbo.FORM_FIELD_DROPDOWN_OPTIONS AS target
USING (
    SELECT
        @Id             AS ID,                 -- Guid (可能是空)
        @DropdownId     AS FORM_FIELD_DROPDOWN_ID,
        @OptionText     AS OPTION_TEXT,
        @OptionValue    AS OPTION_VALUE,
        @OptionTable    AS OPTION_TABLE
) AS source
ON target.ID = source.ID                     -- 只比對 PK
WHEN MATCHED THEN
    UPDATE SET
        OPTION_TEXT  = source.OPTION_TEXT,
        OPTION_VALUE = source.OPTION_VALUE,
        OPTION_TABLE = source.OPTION_TABLE,
        IS_DELETE    = 0
WHEN NOT MATCHED THEN
    INSERT (ID, FORM_FIELD_DROPDOWN_ID, OPTION_TEXT, OPTION_VALUE, OPTION_TABLE, IS_DELETE)
    VALUES (ISNULL(source.ID, NEWID()),       -- 若 Guid.Empty → 直接 NEWID()
            source.FORM_FIELD_DROPDOWN_ID,
            source.OPTION_TEXT,
            source.OPTION_VALUE,
            source.OPTION_TABLE,
            0)
OUTPUT INSERTED.ID;                          -- 把 ID 回傳給 Dapper
";

        public const string InsertOptionIgnoreDuplicate = @"/**/
MERGE dbo.FORM_FIELD_DROPDOWN_OPTIONS AS target
USING (
    SELECT
        @DropdownId  AS FORM_FIELD_DROPDOWN_ID,
        @OptionTable AS OPTION_TABLE,
        @OptionValue AS OPTION_VALUE,
        @OptionText  AS OPTION_TEXT
) AS src
ON target.FORM_FIELD_DROPDOWN_ID = src.FORM_FIELD_DROPDOWN_ID
   AND target.OPTION_TABLE = src.OPTION_TABLE
   AND target.OPTION_VALUE = src.OPTION_VALUE
WHEN NOT MATCHED THEN
    INSERT (ID, FORM_FIELD_DROPDOWN_ID, OPTION_TABLE, OPTION_VALUE, OPTION_TEXT, IS_DELETE)
    VALUES (NEWID(), src.FORM_FIELD_DROPDOWN_ID, src.OPTION_TABLE, src.OPTION_VALUE, src.OPTION_TEXT, 0);
";
        
        public const string DeleteFormMaster = @"
DELETE FROM FORM_FIELD_DROPDOWN_OPTIONS WHERE FORM_FIELD_DROPDOWN_ID IN (
    SELECT ID FROM FORM_FIELD_DROPDOWN WHERE FORM_FIELD_CONFIG_ID IN (
        SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_MASTER_ID = @id
    )
);
DELETE FROM FORM_FIELD_DROPDOWN WHERE FORM_FIELD_CONFIG_ID IN (
    SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_MASTER_ID = @id
);
DELETE FROM FORM_FIELD_VALIDATION_RULE WHERE FIELD_CONFIG_ID IN (
    SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_MASTER_ID = @id
);
DELETE FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_MASTER_ID = @id;
DELETE FROM FORM_FIELD_MASTER WHERE ID = @id;
";

    }
    #endregion
}
