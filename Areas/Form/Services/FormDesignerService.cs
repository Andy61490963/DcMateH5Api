using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Helper;
using System.Net;
using System.Text.RegularExpressions;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
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
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IDropdownSqlSyncService _dropdownSqlSyncService;
    private readonly IReadOnlyList<string> _relationColumnSuffixes;

    public FormDesignerService(
        SQLGenerateHelper sqlHelper,
        SqlConnection connection,
        IConfiguration configuration,
        ISchemaService schemaService,
        IDropdownSqlSyncService dropdownSqlSyncService,
        IOptions<FormSettings> formSettings)
    {
        _con = connection;
        _configuration = configuration;
        _schemaService = schemaService;
        _sqlHelper = sqlHelper;
        _dropdownSqlSyncService = dropdownSqlSyncService;
        _excludeColumns = _configuration.GetSection("DropdownSqlSettings:ExcludeColumns").Get<List<string>>() ?? new();
        _requiredColumns = _configuration.GetSection("FormDesignerSettings:RequiredColumns").Get<List<string>>() ?? new();
        var resolvedSettings = formSettings?.Value ?? new FormSettings();
        _relationColumnSuffixes = resolvedSettings.GetRelationColumnSuffixesOrDefault();
    }

    private readonly List<string> _excludeColumns;
    private readonly List<string> _requiredColumns;
    
    #region Public API
    
    /// <summary>
    /// 取得 FORM_FIELD_Master 列表（可依 SchemaType 與關鍵字模糊查詢）
    /// </summary>
    public Task<List<FormFieldMasterDto>> GetFormMasters(
        FormFunctionType functionType,
        string? q,
        CancellationToken ct)
    {
        var where = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.IS_MASTER_DETAIL!, functionType)
            .AndNotDeleted();

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
    /// 刪除 單一
    /// </summary>
    /// <param name="id"></param>
    public void DeleteFormMaster(Guid id)
    {
        _con.Execute(Sql.DeleteFormMaster, new { id });
    }
    
    /// <summary>
    /// 根據 functionType 取得 主檔維護 或 主明細維護 畫面資料，提供前端建樹狀結構使用。
    /// NotMasterDetail(0) → 只顯示 Base + View；MasterDetail(1) → 顯示 Base + Detail + View。
    /// 若功能模組與表單型態不匹配，直接丟出可讀的 Domain 例外。
    /// </summary>
    public async Task<FormDesignerIndexViewModel> GetFormDesignerIndexViewModel(
        FormFunctionType functionType, 
        Guid? id, 
        CancellationToken ct)
    {
        var master = await GetFormMasterAsync(id, ct) ?? throw new Exception("查無主檔。");

        // 判斷「表單是否主明細」與「功能模組」是否相容
        var isMasterDetail = master.IS_MASTER_DETAIL == FormFunctionType.MasterDetail;
        if (functionType == FormFunctionType.NotMasterDetail && isMasterDetail)
            throw new Exception("此表單為『主明細』型態，無法在『主檔維護』模組開啟。");
        if (functionType == FormFunctionType.MasterDetail && !isMasterDetail)
            throw new Exception("此表單為『非主明細』型態，請改在『主檔維護』模組開啟。");
        if (string.IsNullOrWhiteSpace(master.BASE_TABLE_NAME) || master.BASE_TABLE_ID is null)
            throw new Exception("缺少主檔（Base）表設定：請檢查 BASE_TABLE_NAME / BASE_TABLE_ID。");
        if (string.IsNullOrWhiteSpace(master.VIEW_TABLE_NAME) || master.VIEW_TABLE_ID is null)
            throw new Exception("缺少檢視表（View）表設定：請檢查 VIEW_TABLE_NAME / VIEW_TABLE_ID。");
        
        // 準備回傳物件，先放表頭
        var result = new FormDesignerIndexViewModel
        {
            FormHeader = master,
            BaseFields = null!,
            DetailFields = null!,
            ViewFields = null!
        };

        var baseFields = await GetFieldsByTableName(
            master.BASE_TABLE_NAME,
            master.BASE_TABLE_ID.Value,
            TableSchemaQueryType.OnlyTable);

        result.BaseFields = baseFields;

        // 依功能模組決定要不要載 Detail / View
        if (functionType == FormFunctionType.NotMasterDetail)
        {
            // NotMasterDetail：只載 View（可缺省 → 回傳 null，前端不渲染該節點）
            if (!string.IsNullOrWhiteSpace(master.VIEW_TABLE_NAME) && master.VIEW_TABLE_ID is not null)
            {
                var viewFields = await GetFieldsByTableName(
                    master.VIEW_TABLE_NAME,
                    master.VIEW_TABLE_ID.Value,
                    TableSchemaQueryType.OnlyView);

                result.ViewFields = viewFields;
            }
            // 明細不需要
            result.DetailFields = null!;
        }
        else // MasterDetail
        {
            

            var detailFields = await GetFieldsByTableName(
                master.DETAIL_TABLE_NAME,
                master.DETAIL_TABLE_ID.Value,
                TableSchemaQueryType.OnlyDetail);

            result.DetailFields = detailFields;

            var viewFields = await GetFieldsByTableName(
                master.VIEW_TABLE_NAME,
                master.VIEW_TABLE_ID.Value,
                TableSchemaQueryType.OnlyView);
            result.ViewFields = viewFields;
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
        // 允許英數、底線、點。點是為了支援 schema.name 輸入
        if (!string.IsNullOrWhiteSpace(tableName) &&
            !Regex.IsMatch(tableName, @"^[A-Za-z0-9_\.]+$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("tableName 含非法字元");
        }

        // 解析可能的 schema.name 輸入
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
        var sql = @"SELECT ID FROM FORM_FIELD_Master WHERE ID = @id";
        var res = _con.QueryFirstOrDefault<Guid?>(sql, new { id = model.ID });

        if (res.HasValue)
            return res.Value;

        var insertId = model.ID == Guid.Empty ? Guid.NewGuid() : model.ID;
        static bool HasValue(string? s) => !string.IsNullOrWhiteSpace(s);
        
        _con.Execute(@"
        INSERT INTO FORM_FIELD_Master
    (ID, FORM_NAME, STATUS, SCHEMA_TYPE,
     BASE_TABLE_NAME, VIEW_TABLE_NAME, DETAIL_TABLE_NAME,
     BASE_TABLE_ID,  VIEW_TABLE_ID,  DETAIL_TABLE_ID,
     IS_MASTER_DETAIL, IS_DELETE)
    VALUES
    (@ID, @FORM_NAME, @STATUS, @SCHEMA_TYPE,
     @BASE_TABLE_NAME, @VIEW_TABLE_NAME, @DETAIL_TABLE_NAME,
     @BASE_TABLE_ID,  @VIEW_TABLE_ID,  @DETAIL_TABLE_ID,
     @IS_MASTER_DETAIL, 0);", new
        {
            ID = insertId,
            model.FORM_NAME,
            model.STATUS,
            model.SCHEMA_TYPE,
            model.BASE_TABLE_NAME,
            model.VIEW_TABLE_NAME,
            model.DETAIL_TABLE_NAME,

            BASE_TABLE_ID   = HasValue(model.BASE_TABLE_NAME)   ? insertId : (Guid?)null,
            VIEW_TABLE_ID   = HasValue(model.VIEW_TABLE_NAME)   ? insertId : (Guid?)null,
            DETAIL_TABLE_ID = HasValue(model.DETAIL_TABLE_NAME) ? insertId : (Guid?)null,

            model.IS_MASTER_DETAIL,
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
                FORM_FIELD_Master_ID        = formMasterId ?? Guid.Empty,
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
        
        var masterId = formMasterId ?? configs.Values.FirstOrDefault()?.FORM_FIELD_Master_ID ?? Guid.Empty;

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
    /// 依欄位設定 ID 取得單一欄位設定。
    /// </summary>
    /// <param name="fieldId">欄位設定唯一識別碼</param>
    /// <returns>若找到欄位則回傳 <see cref="FormFieldViewModel"/>；否則回傳 null。</returns>
    public async Task<FormFieldViewModel?> GetFieldById( Guid fieldId )
    {
        var configWhere = new WhereBuilder<FormFieldConfigDto>()
            .AndEq(x => x.ID, fieldId)
            .AndNotDeleted();
        
        var cfg = await _sqlHelper.SelectFirstOrDefaultAsync( configWhere );
        if (cfg == null) return null;

        var masterWhere = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, cfg.FORM_FIELD_Master_ID )
            .AndNotDeleted();
        
        var master = await _sqlHelper.SelectFirstOrDefaultAsync( masterWhere );
        var pk = _schemaService.GetPrimaryKeyColumns( cfg.TABLE_NAME );

        return new FormFieldViewModel
        {
            ID = cfg.ID,
            FORM_FIELD_Master_ID = cfg.FORM_FIELD_Master_ID,
            TableName = cfg.TABLE_NAME,
            COLUMN_NAME = cfg.COLUMN_NAME,
            DATA_TYPE = cfg.DATA_TYPE,
            CONTROL_TYPE = cfg.CONTROL_TYPE,
            CONTROL_TYPE_WHITELIST = FormFieldHelper.GetControlTypeWhitelist(cfg.DATA_TYPE),
            QUERY_COMPONENT_TYPE_WHITELIST = FormFieldHelper.GetQueryConditionTypeWhitelist(cfg.DATA_TYPE),
            IS_REQUIRED = cfg.IS_REQUIRED,
            IS_EDITABLE = cfg.IS_EDITABLE,
            IS_VALIDATION_RULE = HasValidationRules(cfg.ID),
            IS_PK = pk.Contains(cfg.COLUMN_NAME),
            QUERY_DEFAULT_VALUE = cfg.QUERY_DEFAULT_VALUE,
            FIELD_ORDER = cfg.FIELD_ORDER,
            QUERY_COMPONENT = cfg.QUERY_COMPONENT,
            QUERY_CONDITION = cfg.QUERY_CONDITION,
            CAN_QUERY = cfg.CAN_QUERY,
            SchemaType = master.SCHEMA_TYPE
        };
    }

    /// <summary>
    /// 搜尋表格時，如設定檔不存在則先寫入預設欄位設定。
    /// </summary>
    /// <param name="tableName">資料表名稱</param>
    /// <returns>包含欄位設定的 ViewModel</returns>
    public async Task <FormFieldListViewModel?> EnsureFieldsSaved(string tableName, Guid? formMasterId, TableSchemaQueryType schemaType)
    {
        var columns = GetTableSchema(tableName);

        if (columns.Count == 0) return null;

        var missingColumns = _requiredColumns
            .Where(req => !columns.Any(c => c.COLUMN_NAME.Equals(req, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if ((missingColumns.Count > 0 && schemaType == TableSchemaQueryType.OnlyTable) || missingColumns.Count > 0 && schemaType == TableSchemaQueryType.OnlyDetail)
            throw new HttpStatusCodeException(
                HttpStatusCode.BadRequest,
                $"缺少必要欄位：{string.Join(", ", missingColumns)}");

        var master = new FormFieldMasterDto
        {
            FORM_NAME = string.Empty,
            STATUS = (int)TableStatusType.Draft,
            SCHEMA_TYPE = schemaType,
            BASE_TABLE_NAME   = null,
            DETAIL_TABLE_NAME = null,
            VIEW_TABLE_NAME   = null,
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
        }
        
        // 根據傳進來的formMasterId判斷為哪次操作的資料
        var configs = await GetFieldConfigs(tableName, formMasterId);
        var masterId = formMasterId
                       ?? configs.Values.FirstOrDefault()?.FORM_FIELD_Master_ID
                       ?? GetOrCreateFormMasterId(master);
        
        var maxOrder = configs.Values.Any() ? configs.Values.Max(x => x.FIELD_ORDER) : 0;
        var order = maxOrder;
        // 新增還沒存過的欄位
        foreach (var col in columns)
        {
            if (!configs.ContainsKey(col.COLUMN_NAME))
            {
                order++;
                var vm = CreateDefaultFieldConfig(col.COLUMN_NAME, col.DATA_TYPE, masterId, tableName, order, schemaType);
                UpsertField(vm, masterId);
            }
        }

        // 重新查一次所有欄位，確保資料同步
        var result = await GetFieldsByTableName(tableName, masterId, schemaType);

        // var master = _con.QueryFirst<FORM_FIELD_Master>(Sql.FormMasterById, new { id = masterId });
        // result.formName = master.FORM_NAME;
        
        // 對於主檔，先預設有下拉選單的設定，創建的ISUSESQL欄位會為NULL
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
            FORM_FIELD_Master_ID = formMasterId,
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
            .AndEq(x => x.FORM_FIELD_Master_ID, formMasterId)
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
            .AndEq(x => x.FORM_FIELD_Master_ID, formMasterId)
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
        if ( GetTableSchema(baseTableName).Count == 0 )
            throw new InvalidOperationException("主表名稱查無資料");

        if ( GetTableSchema(viewTableName).Count == 0)
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
            STATUS = (int)TableStatusType.Active,
            SCHEMA_TYPE = TableSchemaQueryType.All,
            IS_MASTER_DETAIL = false
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

        if (GetTableSchema(masterTableName).Count == 0)
            throw new InvalidOperationException("主表名稱查無資料");

        if (GetTableSchema(detailTableName).Count == 0)
            throw new InvalidOperationException("明細表名稱查無資料");

        if (GetTableSchema(viewTableName).Count == 0)
            throw new InvalidOperationException("顯示用 View 名稱查無資料");

        // 確保主表與明細表具有共用的關聯欄位，避免 SubmitForm 發生錯誤
        EnsureRelationColumn(masterTableName, detailTableName);

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
            STATUS = (int)TableStatusType.Active,
            SCHEMA_TYPE = TableSchemaQueryType.All,
            IS_MASTER_DETAIL = true
        });
        return id;
    }

    public bool CheckMasterDetailFormMasterExists(Guid masterTableId, Guid detailTableId, Guid viewTableId, Guid? excludeId = null)
    {
        var count = _con.ExecuteScalar<int>(Sql.CheckMasterDetailFormMasterExists,
            new { masterTableId, detailTableId, viewTableId, excludeId });
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
        var configWhere = new WhereBuilder<FormFieldConfigDto>()
            .AndEq(x => x.TABLE_NAME, tableName)
            .AndEq(x => x.FORM_FIELD_Master_ID, formMasterId)
            .AndNotDeleted();
        
        var res = await _sqlHelper.SelectWhereAsync( configWhere );
        return res.ToDictionary(x => x.COLUMN_NAME, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 驗證主表與明細表是否存在共用的關聯欄位。
    /// </summary>
    /// <param name="masterTableName">主表名稱</param>
    /// <param name="detailTableName">明細表名稱</param>
    /// <exception cref="InvalidOperationException">當找不到符合條件的欄位時拋出</exception>
    private void EnsureRelationColumn(string masterTableName, string detailTableName)
    {
        var masterCols = _schemaService.GetFormFieldMaster(masterTableName);
        var detailSet = _schemaService
            .GetFormFieldMaster(detailTableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasRelation = masterCols.Any(columnName =>
            detailSet.Contains(columnName) &&
            _relationColumnSuffixes.MatchesRelationSuffix(columnName));

        if (!hasRelation)
        {
            var suffixDisplay = string.Join("', '", _relationColumnSuffixes);
            throw new InvalidOperationException(
                $"主表與明細表缺少以以下任一結尾的共用關聯欄位：'{suffixDisplay}'");
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
            FORM_FIELD_Master_ID = masterId,
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
FROM FORM_FIELD_Master 
WHERE id = @formMasterId
AND SCHEMA_TYPE = @SchemaType";

        // 20250814，主檔可以和主檔本身自我關聯
        public const string TableSchemaSelect = @"/**/
SELECT COLUMN_NAME, DATA_TYPE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName
ORDER BY ORDINAL_POSITION";

        public const string UpsertFormMaster = @"/**/
MERGE FORM_FIELD_Master AS target
USING (SELECT @ID AS ID) AS src
ON target.ID = src.ID
WHEN MATCHED THEN
    UPDATE SET
        FORM_NAME        = @FORM_NAME,
        BASE_TABLE_NAME  = @BASE_TABLE_NAME,
        VIEW_TABLE_NAME  = @VIEW_TABLE_NAME,
        BASE_TABLE_ID    = @BASE_TABLE_ID,
        VIEW_TABLE_ID    = @VIEW_TABLE_ID,
        IS_MASTER_DETAIL = @IS_MASTER_DETAIL
WHEN NOT MATCHED THEN
    INSERT (
        ID, FORM_NAME, BASE_TABLE_NAME, VIEW_TABLE_NAME,
        BASE_TABLE_ID, VIEW_TABLE_ID, STATUS, SCHEMA_TYPE, IS_MASTER_DETAIL, IS_DELETE)
    VALUES (
        @ID, @FORM_NAME, @BASE_TABLE_NAME, @VIEW_TABLE_NAME,
        @BASE_TABLE_ID, @VIEW_TABLE_ID, @STATUS, @SCHEMA_TYPE, @IS_MASTER_DETAIL, 0)
OUTPUT INSERTED.ID;";

        public const string CheckFormMasterExists = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_Master
WHERE BASE_TABLE_ID = @baseTableId
  AND VIEW_TABLE_ID = @viewTableId
  AND (@excludeId IS NULL OR ID <> @excludeId)";

        public const string UpsertMasterDetailFormMaster = @"/**/
MERGE FORM_FIELD_Master AS target
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
        IS_MASTER_DETAIL  = @IS_MASTER_DETAIL
WHEN NOT MATCHED THEN
    INSERT (
        ID, FORM_NAME, BASE_TABLE_NAME, DETAIL_TABLE_NAME, VIEW_TABLE_NAME,
        BASE_TABLE_ID, DETAIL_TABLE_ID, VIEW_TABLE_ID, STATUS, SCHEMA_TYPE, IS_MASTER_DETAIL, IS_DELETE)
    VALUES (
        @ID, @FORM_NAME, @MASTER_TABLE_NAME, @DETAIL_TABLE_NAME, @VIEW_TABLE_NAME,
        @BASE_TABLE_ID, @DETAIL_TABLE_ID, @VIEW_TABLE_ID, @STATUS, @SCHEMA_TYPE, @IS_MASTER_DETAIL, 0)
OUTPUT INSERTED.ID;";

        public const string CheckMasterDetailFormMasterExists = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_Master
WHERE BASE_TABLE_ID = @masterTableId
  AND DETAIL_TABLE_ID = @detailTableId
  AND VIEW_TABLE_ID = @viewTableId
  AND (@excludeId IS NULL OR ID <> @excludeId)";
        
        public const string UpsertField = @"
MERGE FORM_FIELD_CONFIG AS target
USING (VALUES (@ID)) AS src(ID)
ON target.ID = src.ID
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
        ID, FORM_FIELD_Master_ID, TABLE_NAME, COLUMN_NAME, DATA_TYPE,
        CONTROL_TYPE, IS_REQUIRED, IS_EDITABLE, QUERY_DEFAULT_VALUE, FIELD_ORDER, QUERY_COMPONENT, QUERY_CONDITION, CAN_QUERY, CREATE_TIME, IS_DELETE
    )
    VALUES (
        @ID, @FORM_FIELD_Master_ID, @TABLE_NAME, @COLUMN_NAME, @DATA_TYPE,
        @CONTROL_TYPE, @IS_REQUIRED, @IS_EDITABLE, @QUERY_DEFAULT_VALUE, @FIELD_ORDER, @QUERY_COMPONENT, @QUERY_CONDITION, @CAN_QUERY, GETDATE(), 0
    );";

        public const string CheckFieldExists         = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_CONFIG WHERE ID = @fieldId";

        public const string SetAllEditable = @"/**/
UPDATE FORM_FIELD_CONFIG
SET IS_EDITABLE = @isEditable
WHERE FORM_FIELD_Master_ID = @formMasterId;

-- 若不可編輯，強制取消必填
IF (@isEditable = 0)
BEGIN
    UPDATE FORM_FIELD_CONFIG
    SET IS_REQUIRED = 0
    WHERE FORM_FIELD_Master_ID = @formMasterId;
END
";

        public const string SetAllRequired = @"/**/
UPDATE FORM_FIELD_CONFIG
SET IS_REQUIRED = CASE WHEN @isRequired = 1 AND IS_EDITABLE = 1 THEN 1 ELSE 0 END
WHERE FORM_FIELD_Master_ID = @formMasterId";
        
        public const string CountValidationRules     = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_VALIDATION_RULE WHERE FIELD_CONFIG_ID = @fieldId AND IS_DELETE = 0";

        public const string GetNextValidationOrder   = @"/**/
SELECT ISNULL(MAX(VALIDATION_ORDER), 0) + 1 FROM FORM_FIELD_VALIDATION_RULE WHERE FIELD_CONFIG_ID = @fieldId";
        
        public const string GetControlTypeByFieldId  = @"/**/
SELECT CONTROL_TYPE FROM FORM_FIELD_CONFIG WHERE ID = @fieldId";

        public const string GetRequiredFieldIds      = @"/**/
SELECT FIELD_CONFIG_ID FROM FORM_FIELD_VALIDATION_RULE";

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
        SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @id
    )
);
DELETE FROM FORM_FIELD_DROPDOWN WHERE FORM_FIELD_CONFIG_ID IN (
    SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @id
);
DELETE FROM FORM_FIELD_VALIDATION_RULE WHERE FIELD_CONFIG_ID IN (
    SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @id
);
DELETE FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @id;
DELETE FROM FORM_FIELD_Master WHERE ID = @id;
";

    }
    #endregion
}