using ClassLibrary;
using Dapper;
using DcMateH5Api.Helper;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.Data.SqlClient;

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
    /// <param name="funcType">功能類型</param>
    /// <param name="request">查詢條件與分頁資訊（可選）</param>
    /// <returns>轉換過欄位顯示內容的表單清單資料</returns>
    public List<FormListDataViewModel> GetFormList( FormFunctionType funcType, FormSearchRequest? request = null )
    {
        // 1. 取得所有表單主設定（含欄位設定），使用 VIEW 為主查詢來源
        var metas = _formFieldMasterService.GetFormMetaAggregates( funcType, TableSchemaQueryType.All );

        // 若指定 FormMasterId，僅處理該表單
        if (request?.FormMasterId != Guid.Empty && request?.FormMasterId != null)
        {
            metas = metas.Where(m => m.Master.ID == request.FormMasterId).ToList();
        }

        // 2. 預備回傳結果容器
            var results = new List<FormListDataViewModel>();

        // 3. 對每一個表單主設定進行處理
        foreach (var (master, fieldConfigs) in metas)
        {
            // 3.1 根據 View 撈出該表單的資料列（使用傳入查詢條件與分頁設定）
            var rawRows = _formDataService.GetRows(
                master.VIEW_TABLE_NAME!,
                request?.Conditions,
                request?.Page,
                request?.PageSize);

            // 3.2 取得主表主鍵欄位，用於辨識每一筆資料的唯一性
            var pk = _schemaService.GetPrimaryKeyColumn(master.BASE_TABLE_NAME!);

            if (pk == null)
                throw new InvalidOperationException("No primary key column found");

            // 3.3 將原始資料轉換為擴充型 Row，並解析出 RowId（主鍵值清單）
            var rows = _dropdownService.ToFormDataRows(rawRows, pk, out var rowIds);

            // 3.4 若有資料，且包含 Dropdown 欄位，進行 OptionText 轉換
            if (rowIds.Any())
            {
                // 取得所有 Row 對應的 Dropdown 選項答案（FIELD_ID ➜ OPTION_ID）
                var dropdownAnswers = _dropdownService.GetAnswers(rowIds);

                // 根據答案轉成顯示用文字對應表（OPTION_ID ➜ OPTION_TEXT）
                var optionTextMap = _dropdownService.GetOptionTextMap(dropdownAnswers);

                // 替換每一列資料中的 Dropdown 欄位值為對應的顯示文字
                _dropdownService.ReplaceDropdownIdsWithTexts(rows, fieldConfigs, dropdownAnswers, optionTextMap);
            }

            // 3.5 根據 VIEW 設定，組裝欄位模板（含控制型態、驗證等）
            var fieldTemplates = GetFields(master.VIEW_TABLE_ID, TableSchemaQueryType.OnlyView, master.VIEW_TABLE_NAME!);

            // 3.6 將每一筆資料列組成 ViewModel，並加入回傳清單
            foreach (var row in rows)
            {
                // 將每個欄位設定（template）填入對應的實際值（CurrentValue）
                var rowFields = fieldTemplates
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
                        CurrentValue = row.GetValue(f.Column) // 根據欄位名稱取出該欄位的實際值
                    })
                    // .Where(f =>
                    //     // 1. 排除固定欄位（精確比對）
                    //     !_excludeColumns.Any(col => col.Equals(f.Column, StringComparison.OrdinalIgnoreCase))
                    //
                    //     // 2. 排除 ID/SID 相關欄位（_excludeColumnsId 模糊比對，含 ABC_ID / XYZ_SID）
                    //     && !_excludeColumnsId.Any(col =>
                    //             f.Column.Equals(col, StringComparison.OrdinalIgnoreCase) ||       // 精確
                    //             f.Column.EndsWith($"_{col}", StringComparison.OrdinalIgnoreCase)  // 結尾為 _ID / _SID
                    //     )
                    // )
                    .ToList();
                
                // 組合單筆表單的資料 ViewModel
                results.Add(new FormListDataViewModel
                {
                    FormMasterId = master.ID,
                    BaseId = master.BASE_TABLE_ID,
                    Pk = row.PkId?.ToString() ?? string.Empty,
                    Fields = rowFields
                });
            }
        }

        // 4. 回傳所有表單資料清單（含每列欄位顯示資料）
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
            ?? throw new InvalidOperationException($"FORM_FIELD_Master {formMasterId} not found");

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
        var isUseSql = dropdown?.ISUSESQL ?? false;
        var dropdownId = dropdown?.ID ?? Guid.Empty;

        var options = data.DropdownOptions.Where(o => o.FORM_FIELD_DROPDOWN_ID == dropdownId).ToList();
        List<FormFieldDropdownOptionsDto> finalOptions;

        if (isUseSql && dropdownId != Guid.Empty && !string.IsNullOrWhiteSpace(dropdown?.DROPDOWNSQL))
        {
            if (!dynamicOptionCache.TryGetValue(dropdownId, out finalOptions))
            {
                try
                {
                    var syncResult = _dropdownSqlSyncService.Sync(dropdownId, dropdown!.DROPDOWNSQL);
                    finalOptions = syncResult.Options;
                }
                catch (DropdownSqlSyncException ex)
                {
                    throw new InvalidOperationException($"同步下拉選項失敗（欄位 {field.COLUMN_NAME}）：{ex.Message}", ex);
                }

                dynamicOptionCache[dropdownId] = finalOptions;
            }
        }
        else
        {
            finalOptions = options
                .Where(x => string.IsNullOrWhiteSpace(x.OPTION_TABLE))
                .ToList();
        }

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
            ISUSESQL = isUseSql,
            DROPDOWNSQL = dropdown?.DROPDOWNSQL ?? string.Empty,
            DATA_TYPE = dataType,
            SOURCE_TABLE = schemaType,
            IS_PK = primaryKeys.Contains(field.COLUMN_NAME),
            IS_RELATION = false,
        };
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
            "SELECT ID, COLUMN_NAME, CONTROL_TYPE, DATA_TYPE, IS_EDITABLE, QUERY_COMPONENT, QUERY_CONDITION FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @Id",
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
    }
}
