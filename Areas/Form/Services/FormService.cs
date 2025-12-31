using ClassLibrary;
using Dapper;
using DcMateH5Api.Helper;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace DcMateH5Api.Areas.Form.Services;

public class FormService : IFormService
{
    private readonly SqlConnection _con;
    private readonly ITransactionService _txService;
    private readonly IFormFieldMasterService _formFieldMasterService;
    private readonly ISchemaService _schemaService;
    private readonly IFormFieldConfigService _formFieldConfigService;
    private readonly IFormDataService _formDataService;
    private readonly IDropdownService _dropdownService;
    private readonly IDropdownSqlSyncService _dropdownSqlSyncService;
    private readonly IConfiguration _configuration;
    
    public FormService(SqlConnection connection, ITransactionService txService, IFormFieldMasterService formFieldMasterService, ISchemaService schemaService, IFormFieldConfigService formFieldConfigService, IDropdownService dropdownService, IFormDataService formDataService, IConfiguration configuration, IDropdownSqlSyncService dropdownSqlSyncService)
    {
        _con = connection;
        _txService = txService;
        _formFieldMasterService = formFieldMasterService;
        _schemaService = schemaService;
        _formFieldConfigService = formFieldConfigService;
        _formDataService = formDataService;
        _dropdownService = dropdownService;
        _dropdownSqlSyncService = dropdownSqlSyncService;
        _configuration = configuration;
        _excludeColumns = _configuration.GetSection("FormDesignerSettings:RequiredColumns").Get<List<string>>() ?? new();
        _excludeColumnsId = _configuration.GetSection("DropdownSqlSettings:ExcludeColumns").Get<List<string>>() ?? new();
    }
    
    private readonly List<string> _excludeColumns;
    private readonly List<string> _excludeColumnsId;
    
    /// <summary>
    /// 取得所有表單的資料清單（含對應欄位值），
    /// 並自動轉換下拉選單欄位的選項 ID 為顯示文字（OptionText）。
    /// </summary>
    /// <remarks>
    /// 【設計說明】
    ///
    /// 此方法為「表單列表頁」的核心組裝邏輯，
    /// 負責整合以下幾件事：
    ///
    /// 1. 依功能類型取得表單主設定（FORM_FIELD_Master + 欄位設定）
    /// 2. 透過 VIEW_TABLE 讀取實際資料列（避免直接查 BASE_TABLE）
    /// 3. 自動處理 Dropdown 欄位：
    ///    - 將資料表中的 OptionId
    ///    - 轉換為前端顯示用的 OptionText
    /// 4. 將「欄位模板（設定）」與「實際資料值」組合成 ViewModel
    ///
    /// 【為什麼用 VIEW 查資料？】
    /// - VIEW 已封裝 Join / 欄位對應邏輯
    /// - 可避免在此方法中處理複雜 SQL
    /// - 與動態表單「設定驅動」的設計理念一致
    ///
    /// 【注意事項】
    /// - VIEW 必須包含 BASE_TABLE 的 PK 欄位，否則無法正確組 RowId
    /// - Dropdown 相關轉換會依賴 rowIds，若 PK 遺失會直接丟 Exception
    /// </remarks>
    /// <param name="funcType">功能類型（決定可用的表單集合）</param>
    /// <param name="request">查詢條件與分頁資訊（可選）</param>
    /// <returns>轉換過欄位顯示內容的表單清單資料</returns>
    public List<FormListDataViewModel> GetFormList(FormFunctionType funcType, FormSearchRequest? request = null)
    {
        // ------------------------------------------------------------
        // 1. 取得表單主設定（含欄位設定）
        // ------------------------------------------------------------
        // 回傳結構為：
        // IEnumerable<(FormMaster master, List<FormFieldConfig> fieldConfigs)>
        // 每一組代表一張「表單定義」
        var metas = _formFieldMasterService.GetFormMetaAggregates(
            funcType,
            TableSchemaQueryType.All
        );

        // ------------------------------------------------------------
        // 1.1 若指定 FormMasterId，僅處理該表單
        // ------------------------------------------------------------
        // 常見於：
        // - 單一表單列表頁
        // - 後台指定某張表單查詢
        if (request?.FormMasterId != null && request.FormMasterId != Guid.Empty)
        {
            metas = metas
                .Where(m => m.Master.ID == request.FormMasterId)
                .ToList();
        }

        // ------------------------------------------------------------
        // 2. 準備最終回傳結果容器
        // ------------------------------------------------------------
        // 注意：這是一個「跨多張表單」的平坦清單
        var results = new List<FormListDataViewModel>();

        // ------------------------------------------------------------
        // 3. 逐一處理每一張表單設定
        // ------------------------------------------------------------
        foreach (var (master, fieldConfigs) in metas)
        {
            // --------------------------------------------------------
            // 0. 判斷是否為「多對多維護模式」
            // --------------------------------------------------------
            var isMultipleMappingMaintenance =
                funcType == FormFunctionType.MultipleMappingMaintenance;

            // --------------------------------------------------------
            // 1. 決定資料來源表（查資料用）
            // --------------------------------------------------------
            var dataTableName = isMultipleMappingMaintenance
                ? master.BASE_TABLE_NAME!     // 直接查主表
                : master.VIEW_TABLE_NAME!;    // 一般模式查 VIEW

            // --------------------------------------------------------
            // 2. 決定 Schema 來源（欄位模板用）
            // --------------------------------------------------------
            var schemaTableId = isMultipleMappingMaintenance
                ? master.BASE_TABLE_ID        // 主表 schema
                : master.VIEW_TABLE_ID;       // 檢視表 schema

            var schemaQueryType = isMultipleMappingMaintenance
                ? TableSchemaQueryType.OnlyTable   // 對應 BASE_TABLE
                : TableSchemaQueryType.OnlyView;   // 原本邏輯

            // --------------------------------------------------------
            // 3. 讀取實際資料列
            // --------------------------------------------------------
            var rawRows = _formDataService.GetRows(
                dataTableName,
                request?.Conditions,
                request?.Page,
                request?.PageSize
            );

            // --------------------------------------------------------
            // 4. 一律用 BASE_TABLE 的 PK 當 RowId
            // --------------------------------------------------------
            var pk = _schemaService.GetPrimaryKeyColumn(master.BASE_TABLE_NAME!);
            if (pk == null)
            {
                throw new InvalidOperationException(
                    $"缺失 PK 欄位，請檢查 BASE_TABLE_NAME -> [{master.BASE_TABLE_NAME}]"
                );
            }

            var rows = _dropdownService.ToFormDataRows(
                rawRows,
                pk,
                out var rowIds
            );

            // --------------------------------------------------------
            // 5. Dropdown 處理
            // --------------------------------------------------------
            if (rowIds.Any())
            {
                var dropdownAnswers = _dropdownService.GetAnswers(rowIds);
                var optionTextMap = _dropdownService.GetOptionTextMap(dropdownAnswers);

                _dropdownService.ReplaceDropdownIdsWithTexts(
                    rows,
                    fieldConfigs,
                    dropdownAnswers,
                    optionTextMap
                );
            }

            // --------------------------------------------------------
            // 6. 取得欄位模板（依 schemaQueryType）
            // --------------------------------------------------------
            var fieldTemplates = GetFields(
                schemaTableId,
                schemaQueryType,
                dataTableName
            );

            // --------------------------------------------------------
            // 7. SID 欄位隱藏規則
            // --------------------------------------------------------
            var sidColumnsToHide = isMultipleMappingMaintenance
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) // BASE 模式不需要隱藏
                : GetCommonSidColumnsToHide(master.VIEW_TABLE_NAME!, master.BASE_TABLE_NAME!);

            // --------------------------------------------------------
            // 8. 組 ViewModel
            // --------------------------------------------------------
            foreach (var row in rows)
            {
                if (row.PkId == null)
                {
                    throw new InvalidOperationException(
                        $"缺失 PK 欄位，請檢查資料來源表 -> [{dataTableName}]"
                    );
                }

                var rowFields = fieldTemplates
                    .Where(f => !sidColumnsToHide.Contains(f.Column))
                    .Select(f => new FormFieldInputViewModel
                    {
                        FieldConfigId = f.FieldConfigId,
                        Column = f.Column,
                        DATA_TYPE = f.DATA_TYPE,
                        CONTROL_TYPE = f.CONTROL_TYPE,
                        CAN_QUERY = f.CAN_QUERY,
                        QUERY_COMPONENT = f.QUERY_COMPONENT,
                        QUERY_CONDITION = f.QUERY_CONDITION,
                        DefaultValue = f.DefaultValue,
                        IS_REQUIRED = f.IS_REQUIRED,
                        IS_EDITABLE = f.IS_EDITABLE,
                        IS_PK = f.IS_PK,
                        IS_RELATION = f.IS_RELATION,
                        ValidationRules = f.ValidationRules,
                        ISUSESQL = f.ISUSESQL,
                        DROPDOWNSQL = f.DROPDOWNSQL,
                        OptionList = f.OptionList,
                        SOURCE_TABLE = f.SOURCE_TABLE,
                        CurrentValue = row.GetValue(f.Column)
                    })
                    .ToList();

                results.Add(new FormListDataViewModel
                {
                    FormMasterId = master.ID,
                    BaseId = master.BASE_TABLE_ID,
                    Pk = row.PkId.ToString(),
                    Fields = rowFields
                });
            }
        }
        
        // ------------------------------------------------------------
        // 4. 回傳完整表單資料清單
        // ------------------------------------------------------------
        return results;
    }
    
    /// <summary>
    /// 根據表單設定抓取主表欄位與現有資料（編輯時用）
    /// 只對主表進行欄位組裝，Dropdown 顯示選項答案
    /// </summary>
    public FormSubmissionViewModel GetFormSubmission(Guid? formMasterId, string? pk = null)
    {
        // 1. 查主設定
        var master = _formFieldMasterService.GetFormFieldMasterFromId(formMasterId)
            ?? throw new InvalidOperationException($"FORM_FIELD_MASTER {formMasterId} not found");

        // 2. 依據 SchemaType 解析目標表資訊
        var (targetId, targetTable, schemaType) = ResolveTargetTable(master);

        // 3. 取得欄位設定
        var fields = GetFields(targetId, schemaType, targetTable);

        // 4. 撈實際資料（如果是編輯模式）
        IDictionary<string, object?>? dataRow = null;
        Dictionary<Guid, Guid>? dropdownAnswers = null;

        if (!string.IsNullOrWhiteSpace(pk))
        {
            // 4.1 取得主鍵名稱/型別/值
            var (pkName, pkType, pkValue) = _schemaService.ResolvePk(targetTable, pk);

            // 4.2 查詢主表資料（參數化防注入）
            var sql = $"SELECT * FROM [{targetTable}] WHERE [{pkName}] = @id";
            dataRow = _con.QueryFirstOrDefault(sql, new { id = pkValue }) as IDictionary<string, object?>;

            // 4.3 如果有 Dropdown 欄位，再查一次答案
            if (fields.Any(f => f.CONTROL_TYPE == FormControlType.Dropdown))
            {
                dropdownAnswers = _con.Query<(Guid FieldId, Guid OptionId)>(
                    @"/**/SELECT FORM_FIELD_CONFIG_ID AS FieldId, FORM_FIELD_DROPDOWN_OPTIONS_ID AS OptionId
                      FROM FORM_FIELD_DROPDOWN_ANSWER WHERE ROW_ID = @Pk",
                    new { Pk = pk })
                    .ToDictionary(x => x.FieldId, x => x.OptionId);
            }
        }

        // 5. 組裝欄位現值
        foreach (var field in fields)
        {
            if (field.CONTROL_TYPE == FormControlType.Dropdown && dropdownAnswers?.TryGetValue(field.FieldConfigId, out var optId) == true)
            {
                field.CurrentValue = optId;
            }
            else if (dataRow?.TryGetValue(field.Column, out var val) == true)
            {
                field.CurrentValue = val;
            }
            // else 預設 null（新增模式或沒有資料）
        }

        // 6. 回傳組裝後 ViewModel
        return new FormSubmissionViewModel
        {
            FormId = master.ID,
            Pk = pk,
            TargetTableToUpsert = targetTable,
            FormName = master.FORM_NAME,
            Fields = fields
        };
    }

    private (Guid? Id, string Table, TableSchemaQueryType Schema) ResolveTargetTable(FormFieldMasterDto masterDto)
    {
        return masterDto.SCHEMA_TYPE switch
        {
            TableSchemaQueryType.OnlyTable => (masterDto.BASE_TABLE_ID, masterDto.BASE_TABLE_NAME ?? throw new InvalidOperationException("BASE_TABLE_NAME missing"), TableSchemaQueryType.OnlyTable),
            TableSchemaQueryType.OnlyDetail => (masterDto.DETAIL_TABLE_ID, masterDto.DETAIL_TABLE_NAME ?? throw new InvalidOperationException("DETAIL_TABLE_NAME missing"), TableSchemaQueryType.OnlyDetail),
            TableSchemaQueryType.OnlyView => (masterDto.VIEW_TABLE_ID, masterDto.VIEW_TABLE_NAME ?? throw new InvalidOperationException("VIEW_TABLE_NAME missing"), TableSchemaQueryType.OnlyView),
            _ => throw new InvalidOperationException("Unsupported schema type")
        };
    }

    /// <summary>
    /// 取得 欄位
    /// </summary>
    /// <param name="masterId"></param>
    /// <returns></returns>
   private List<FormFieldInputViewModel> GetFields(Guid? masterId, TableSchemaQueryType schemaType, string tableName)
    {
        var columnTypes = _formDataService.LoadColumnTypes(tableName);
        var configData = _formFieldConfigService.LoadFieldConfigData(masterId);
        var primaryKeys = _schemaService.GetPrimaryKeyColumns(tableName);

        
        // 只保留可編輯欄位，將不可編輯欄位直接過濾掉以避免出現在前端
        var editableConfigs = configData.FieldConfigs;
            // .Where(cfg => cfg.IS_EDITABLE)
            // .ToList();

        var dynamicOptionCache = new Dictionary<Guid, List<FormFieldDropdownOptionsDto>>();

        return editableConfigs
            .Select(cfg => BuildFieldViewModel(cfg, configData, columnTypes, schemaType, dynamicOptionCache, primaryKeys))
            .ToList();
    }

    private FormFieldInputViewModel BuildFieldViewModel(
        FormFieldConfigDto field,
        FieldConfigData data,
        Dictionary<string, string> columnTypes,
        TableSchemaQueryType schemaType,
        Dictionary<Guid, List<FormFieldDropdownOptionsDto>> dynamicOptionCache,
        HashSet<string> primaryKeys)
    {
        var dropdown = data.DropdownConfigs.FirstOrDefault(d => d.FORM_FIELD_CONFIG_ID == field.ID);

        var finalOptions = ResolveDropdownOptions(
            dropdown,
            data,
            dynamicOptionCache,
            field.COLUMN_NAME);

        var rules = data.ValidationRules
            .Where(r => r.FIELD_CONFIG_ID == field.ID)
            .OrderBy(r => r.VALIDATION_ORDER)
            .ToList();

        var dataType = columnTypes.TryGetValue(field.COLUMN_NAME, out var dtype)
            ? dtype
            : "nvarchar";

        return new FormFieldInputViewModel
        {
            FieldConfigId = field.ID,
            Column = field.COLUMN_NAME,

            CONTROL_TYPE = field.CONTROL_TYPE,
            QUERY_COMPONENT = field.QUERY_COMPONENT,
            QUERY_CONDITION = field.QUERY_CONDITION,
            CAN_QUERY = field.CAN_QUERY,

            DefaultValue = field.QUERY_DEFAULT_VALUE,
            IS_REQUIRED = field.IS_REQUIRED,
            IS_EDITABLE = field.IS_EDITABLE,

            ValidationRules = rules,
            OptionList = finalOptions,

            // 這兩個是 dropdown 設定資訊（前端可能要顯示設定）
            ISUSESQL = dropdown?.ISUSESQL ?? false,
            DROPDOWNSQL = dropdown?.DROPDOWNSQL ?? string.Empty,

            DATA_TYPE = dataType,
            SOURCE_TABLE = schemaType,
            IS_PK = primaryKeys.Contains(field.COLUMN_NAME),
            IS_RELATION = false,
        };
    }

    private List<FormFieldDropdownOptionsDto> ResolveDropdownOptions(
        FormDropDownDto? dropdown,
        FieldConfigData data,
        Dictionary<Guid, List<FormFieldDropdownOptionsDto>> dynamicOptionCache,
        string columnNameForErrorMessage)
    {
        if (dropdown is null)
            return new List<FormFieldDropdownOptionsDto>();

        var dropdownId = dropdown.ID;
        if (dropdownId == Guid.Empty)
            return new List<FormFieldDropdownOptionsDto>();

        var dropdownSql = dropdown.DROPDOWNSQL;

        // QueryDropdown 優先
        if (dropdown.ISUSESQL && dropdown.IS_QUERY_DROPDOWN && !string.IsNullOrWhiteSpace(dropdownSql))
        {
            var names = GetPreviousQueryList(dropdown);

            return BuildQueryDropdownOptions(dropdownId, names);
        }

        // SQL dropdown（ID/NAME）
        if (dropdown.ISUSESQL && !string.IsNullOrWhiteSpace(dropdownSql))
        {
            return GetOrSyncSqlDropdownOptions(dropdownId, dropdownSql, dynamicOptionCache, columnNameForErrorMessage);
        }

        // 靜態 dropdown
        return GetStaticDropdownOptions(data, dropdownId);
    }

    private static List<FormFieldDropdownOptionsDto> BuildQueryDropdownOptions(Guid dropdownId, List<string> names)
    {
        return names
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new FormFieldDropdownOptionsDto
            {
                ID = Guid.Empty,
                FORM_FIELD_DROPDOWN_ID = dropdownId,
                OPTION_TABLE = null,
                OPTION_TEXT = name!,
                OPTION_VALUE = name!
            })
            .ToList();
    }

    private List<FormFieldDropdownOptionsDto> GetOrSyncSqlDropdownOptions(
        Guid dropdownId,
        string dropdownSql,
        Dictionary<Guid, List<FormFieldDropdownOptionsDto>> dynamicOptionCache,
        string columnNameForErrorMessage)
    {
        if (dynamicOptionCache.TryGetValue(dropdownId, out var cached))
            return cached;

        try
        {
            var syncResult = _dropdownSqlSyncService.Sync(dropdownId, dropdownSql);
            var options = syncResult.Options;

            dynamicOptionCache[dropdownId] = options;
            return options;
        }
        catch (DropdownSqlSyncException ex)
        {
            throw new InvalidOperationException($"同步下拉選項失敗（欄位 {columnNameForErrorMessage}）：{ex.Message}", ex);
        }
    }

    private static List<FormFieldDropdownOptionsDto> GetStaticDropdownOptions(FieldConfigData data, Guid dropdownId)
    {
        return data.DropdownOptions
            .Where(o => o.FORM_FIELD_DROPDOWN_ID == dropdownId)
            .Where(o => string.IsNullOrWhiteSpace(o.OPTION_TABLE))
            .ToList();
    }
    
    /// <summary>
    /// 解析使用者先前查詢的下拉值清單（由 ImportPreviousQueryDropdownValues 寫入 SQL）。
    /// </summary>
    /// <param name="dropdown">下拉選單設定主檔</param>
    /// <returns>先前查詢結果的 NAME 值清單</returns>
    private List<string> GetPreviousQueryList(FormDropDownDto? dropdown)
    {
        if (dropdown is null || !dropdown.IS_QUERY_DROPDOWN)
            return new List<string>();

        var sourceSql = dropdown.DROPDOWNSQL;
        if (string.IsNullOrWhiteSpace(sourceSql))
            return new List<string>();

        var wrappedSql = $@"/**/
SELECT src.[NAME]
FROM (
{sourceSql}
) AS src;";

        var wasClosed = _con.State != System.Data.ConnectionState.Open;
        if (wasClosed)
            _con.Open();

        try
        {
            var values = _con.Query<string>(
                    wrappedSql,
                    transaction: null,
                    commandTimeout: 10)
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return values;
        }
        catch
        {
            return new List<string>();
        }
        finally
        {
            if (wasClosed)
                _con.Close();
        }
    }
    
    /// <summary>
    /// 儲存或更新表單資料（含下拉選項答案），由呼叫端決定交易界限。
    /// </summary>
    /// <param name="input">前端送出的表單資料</param>
    /// <param name="tx">交易物件</param>
    public void SubmitForm(FormSubmissionInputModel input, SqlTransaction tx)
    {
        SubmitFormCore(input, tx);
    }

    /// <summary>
    /// 儲存或更新表單資料（含下拉選項答案），由本服務自行開啟交易。
    /// </summary>
    /// <param name="input">前端送出的表單資料</param>
    public void SubmitForm(FormSubmissionInputModel input)
    {
        _txService.WithTransaction(tx => SubmitFormCore(input, tx));
    }

    /// <summary>
    /// 實際執行資料存取的核心邏輯，所有資料庫操作均以同一個交易物件進行。
    /// </summary>
    private void SubmitFormCore(FormSubmissionInputModel input, SqlTransaction tx)
    {
        // 查表單主設定
        var master = _formFieldMasterService.GetFormFieldMasterFromId(input.BaseId, tx);

        var (targetId, targetTable, _) = ResolveTargetTable(master);

        // 查欄位設定並帶出 IS_EDITABLE 欄位，後續用於權限檢查
        var configs = _con.Query<FormFieldConfigDto>(
            "SELECT ID, COLUMN_NAME, CONTROL_TYPE, DATA_TYPE, IS_EDITABLE, QUERY_COMPONENT, QUERY_CONDITION FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_MASTER_ID = @Id",
            new { Id = targetId },
            transaction: tx).ToDictionary(x => x.ID);

        // 1. 欄位 mapping & 型別處理
        var (normalFields, dropdownAnswers) = MapInputFields(input.InputFields, configs);

        // 2. Insert/Update 決策
        var (pkName, pkType, typedRowId) = _schemaService.ResolvePk(targetTable, input.Pk, tx);
        bool isInsert = string.IsNullOrEmpty(input.Pk);
        bool isIdentity = _schemaService.IsIdentityColumn(targetTable, pkName, tx);
        object? realRowId = typedRowId;

        if (isInsert)
            realRowId = InsertRow(targetTable, pkName, pkType, isIdentity, normalFields, tx);
        else
            UpdateRow(targetTable, pkName, normalFields, realRowId, tx);

        // 3. Dropdown Upsert
        foreach (var (configId, optionId) in dropdownAnswers)
        {
            _con.Execute(Sql.UpsertDropdownAnswer, new { configId, RowId = realRowId, optionId }, transaction: tx);
        }
    }
    
    private (List<(string Column, object? Value)> NormalFields,
        List<(Guid ConfigId, Guid OptionId)> DropdownAnswers)
        MapInputFields(IEnumerable<FormInputField> inputFields,
            IReadOnlyDictionary<Guid, FormFieldConfigDto> configs)
    {
        var normal = new List<(string Column, object? Value)>();
        var ddAns  = new List<(Guid ConfigId, Guid OptionId)>();

        foreach (var field in inputFields)
        {
            if (!configs.TryGetValue(field.FieldConfigId, out var cfg))
                continue;                               // 找不到設定直接忽略

            // 欄位若設定為不可編輯，直接忽略以防止未授權修改
            if (!cfg.IS_EDITABLE)
                continue;

            // // --- 必填檢查 ---
            // if (cfg.IS_REQUIRED && string.IsNullOrWhiteSpace(field.Value))
            //     throw new ValidationException($"欄位「{cfg.COLUMN_NAME}」為必填。");
            //
            // if (string.IsNullOrEmpty(field.Value))
            //     continue;

            if (cfg.CONTROL_TYPE == FormControlType.Dropdown)
            {
                if (Guid.TryParse(field.Value, out var optId))
                    ddAns.Add((cfg.ID, optId));
            }
            else
            {
                var val = ConvertToColumnTypeHelper.Convert(cfg.DATA_TYPE, field.Value);
                normal.Add((cfg.COLUMN_NAME, val));
            }
        }
        return (normal, ddAns);
    }

    /// <summary>
    /// 實作 INSERT 資料邏輯，支援 Identity 與非 Identity 主鍵模式
    /// </summary>
    private object InsertRow(
        string tableName,
        string pkName,
        string pkType,
        bool isIdentity,
        List<(string Column, object? Value)> normalFields,
        SqlTransaction tx
    )
    {
        const string RowIdParamName = "ROWID";
        var columns = new List<string>();
        var values = new List<string>();
        var paramDict = new Dictionary<string, object>();

        // 若主鍵非 Identity，手動產生主鍵值
        if (!isIdentity)
        {
            var newId = GeneratePkValueHelper.GeneratePkValue(pkType); // 支援 Guid / int / string 等
            columns.Add($"[{pkName}]");
            values.Add($"@{RowIdParamName}");
            paramDict[RowIdParamName] = newId!;
        }

        int i = 0;
        foreach (var field in normalFields)
        {
            if (string.Equals(field.Column, pkName, StringComparison.OrdinalIgnoreCase))
                continue;

            var paramName = $"VAL{i++}";
            columns.Add($"[{field.Column}]");
            values.Add($"@{paramName}");
            paramDict[paramName] = field.Value;
        }

        string sql;
        object? resultId;

        if (isIdentity && !normalFields.Any())
        {
            sql = $@"
                INSERT INTO [{tableName}] DEFAULT VALUES;
                SELECT CAST(SCOPE_IDENTITY() AS {pkType});";

            resultId = _con.ExecuteScalar(sql, transaction: tx);
        }
        else if (isIdentity)
        {
            sql = $@"
                INSERT INTO [{tableName}]
                    ({string.Join(", ", columns)})
                OUTPUT INSERTED.[{pkName}]
                VALUES ({string.Join(", ", values)})";

            resultId = _con.ExecuteScalar(sql, paramDict, tx); 
        }
        else
        {
            sql = $@"
                INSERT INTO [{tableName}]
                    ({string.Join(", ", columns)})
                VALUES ({string.Join(", ", values)})";

            _con.Execute(sql, paramDict, tx); 
            resultId = paramDict[RowIdParamName];
        }

        return resultId!;
    }
    
    /// <summary>
    /// 動態產生並執行 UPDATE 語法，用於更新資料表中的指定主鍵資料列。
    /// </summary>
    /// <param name="tableName">目標資料表名稱</param>
    /// <param name="pkName">主鍵欄位名稱</param>
    /// <param name="normalFields">需要更新的欄位集合（欄位名與新值）</param>
    /// <param name="realRowId">實際的主鍵值（用於 WHERE 條件）</param>
    /// <param name="tx">交易物件</param>
    private void UpdateRow(
        string tableName,
        string pkName,
        List<(string Column, object? Value)> normalFields,
        object realRowId,
        SqlTransaction tx)
    {
        // 若無更新欄位，直接結束，不執行 SQL
        if (!normalFields.Any()) return;

        // 動態產生 SET 子句，並準備對應參數字典
        var setList = new List<string>();
        var paramDict = new Dictionary<string, object> { ["ROWID"] = realRowId! };

        int i = 0;
        foreach (var field in normalFields)
        {
            // 每一個欄位會對應一組參數：VAL0、VAL1、... 以避免參數衝突
            var paramName = $"VAL{i}";
            setList.Add($"[{field.Column}] = @{paramName}");         // 欄位名用中括號包起來避免保留字
            paramDict[paramName] = field.Value ?? null;              // 允許欄位值為 null
            i++;
        }

        // 組合最終 SQL 語句：UPDATE 表 SET 欄位1 = @, 欄位2 = @ ... WHERE 主鍵 = @ROWID
        var sql = $@"
        UPDATE [{tableName}]
        SET {string.Join(", ", setList)}
        WHERE [{pkName}] = @ROWID";

        _con.Execute(sql, paramDict, transaction: tx);
    }
    
    public void PhysicalDeleteByBaseTableId(Guid baseTableId, string pk)
    {
        if (baseTableId == Guid.Empty) throw new ArgumentException("BaseId 不可為空", nameof(baseTableId));
        if (string.IsNullOrWhiteSpace(pk)) throw new ArgumentException("Pk 不可為空", nameof(pk));

        _txService.WithTransaction(tx => PhysicalDeleteByBaseTableIdCore(baseTableId, pk, tx));
    }

    /// <summary>
    /// 物理性刪除資料
    /// </summary>
    /// <param name="baseTableId"></param>
    /// <param name="pk"></param>
    /// <param name="tx"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private void PhysicalDeleteByBaseTableIdCore(Guid baseTableId, string pk, SqlTransaction tx)
    {
        // 1) 由 BaseTableId 找到實際資料表名（最關鍵）
        var tableName = _schemaService.GetTableNameByTableId(baseTableId, tx);

        // 2) 解析該表 PK（型別要對，避免你拿字串去比 int）
        var (pkName, _, typedPk) = _schemaService.ResolvePk(tableName, pk, tx);

        // 3) 防 identifier 注入（表名/欄位名不是參數化能救的）
        ValidateSqlIdentifier(tableName);
        ValidateSqlIdentifier(pkName);

        // 4) 先刪 dropdown answer（限定 baseTableId 範圍，避免撞號誤刪）
        _con.Execute(Sql.DeleteDropdownAnswersByBaseTableIdAndRowId,
            new { BaseTableId = baseTableId, RowId = typedPk },
            transaction: tx);

        // 5) 再刪實際資料表
        var deleteSql = $@"
DELETE FROM [{tableName}]
WHERE [{pkName}] = @RowId;";

        var affected = _con.Execute(deleteSql, new { RowId = typedPk }, transaction: tx);

        if (affected == 0)
            throw new InvalidOperationException($"找不到要刪除的資料：{tableName}.{pkName} = {pk}");
    }

    private static void ValidateSqlIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new InvalidOperationException("識別字不可為空");

        foreach (var ch in identifier)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '_'))
                throw new InvalidOperationException($"不合法的識別字：{identifier}");
        }
    }
    
    /// <summary>
    /// 取得「View 與 Base 同名」且「欄位結尾為 _sid」的欄位集合（忽略大小寫）
    /// 用途：在 GetFormList 回傳 Fields 時隱藏這些欄位（避免前端看到重複 FK）
    /// </summary>
    private HashSet<string> GetCommonSidColumnsToHide(string viewTableName, string baseTableName, SqlTransaction? tx = null)
    {
        // 取得 View/Base 欄位清單（INFORMATION_SCHEMA.COLUMNS）
        var viewCols = _schemaService.GetFormFieldMaster(viewTableName, tx);
        var baseCols = _schemaService.GetFormFieldMaster(baseTableName, tx);

        // 用 HashSet 加速查詢（忽略大小寫）
        var baseSet = baseCols.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 交集 + 結尾 _SID ， 這邊懶得抽出去了
        return viewCols
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Where(c => c.EndsWith("_SID", StringComparison.OrdinalIgnoreCase))
            .Where(c => baseSet.Contains(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

private static class Sql
    {
        public const string UpsertDropdownAnswer = @"
MERGE FORM_FIELD_DROPDOWN_ANSWER AS target
USING (SELECT @ConfigId AS FORM_FIELD_CONFIG_ID, @RowId AS ROW_ID) AS src
    ON target.FORM_FIELD_CONFIG_ID = src.FORM_FIELD_CONFIG_ID AND target.ROW_ID = src.ROW_ID
WHEN MATCHED THEN
    UPDATE SET FORM_FIELD_DROPDOWN_OPTIONS_ID = @OptionId
WHEN NOT MATCHED THEN
    INSERT (ID, FORM_FIELD_CONFIG_ID, FORM_FIELD_DROPDOWN_OPTIONS_ID, ROW_ID)
    VALUES (NEWID(), src.FORM_FIELD_CONFIG_ID, @OptionId, src.ROW_ID);";
        
        public const string DeleteDropdownAnswersByBaseTableIdAndRowId = @"
/**/
DELETE ans
FROM FORM_FIELD_DROPDOWN_ANSWER ans
JOIN FORM_FIELD_CONFIG cfg
  ON cfg.ID = ans.FORM_FIELD_CONFIG_ID
WHERE cfg.FORM_FIELD_MASTER_ID = @BaseTableId
  AND ans.ROW_ID = @RowId;";
    }
}
