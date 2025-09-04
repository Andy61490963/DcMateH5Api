using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Helper;
using System.Net;
using System.Text.RegularExpressions;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Areas.Permission.Mappers;
using DcMateH5Api.SqlHelper;
using Microsoft.Data.SqlClient;
using System.Threading;

namespace DcMateH5Api.Areas.Form.Services;

public class FormDesignerService : IFormDesignerService
{
    private readonly SqlConnection _con;
    private readonly IConfiguration _configuration;
    private readonly ISchemaService _schemaService;
    private readonly SQLGenerateHelper _sqlHelper;

    public FormDesignerService(SQLGenerateHelper sqlHelper, SqlConnection connection, IConfiguration configuration, ISchemaService schemaService)
    {
        _con = connection;
        _configuration = configuration;
        _schemaService = schemaService;
        _sqlHelper = sqlHelper;
        _excludeColumns = _configuration.GetSection("DropdownSqlSettings:ExcludeColumns").Get<List<string>>() ?? new();
        _requiredColumns = _configuration.GetSection("FormDesignerSettings:RequiredColumns").Get<List<string>>() ?? new();
    }

    private readonly List<string> _excludeColumns;
    private readonly List<string> _requiredColumns;
    
    #region Public API
    
    /// <summary>
    /// 取得 列表
    /// </summary>
    /// <returns></returns>
    public Task<List<FORM_FIELD_Master>> GetFormMasters(TableSchemaQueryType schemaType, CancellationToken ct)
    {
        // var statusList = new[] { TableStatusType.Active };
        var where = new WhereBuilder<FORM_FIELD_Master>()
            // .AndIn(x => x.STATUS, statusList)
            .AndEq(x => x.SCHEMA_TYPE, schemaType)
            .AndNotDeleted();
        
        var result = _sqlHelper.SelectWhereAsync(where, ct);
        return result;
    }

    public Task UpdateFormMaster(FORM_FIELD_Master model, CancellationToken ct)
    {
        return _sqlHelper.UpdateAllByIdAsync(model, UpdateNullBehavior.IgnoreNulls, false, ct);
    }

    public Task UpdateFormName(Guid id, string formName, CancellationToken ct)
    {
        const string sql = "UPDATE FORM_FIELD_Master SET FORM_NAME = @formName WHERE ID = @id";
        _con.Execute(sql, new { id, formName });
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 取得單一主表設定
    /// </summary>
    /// <param name="id">主表ID</param>
    /// <param name="ct">取消權杖</param>
    /// <returns></returns>
    private Task<FORM_FIELD_Master?> GetFormMasterAsync(Guid? id, CancellationToken ct)
    {
        if (id == null) return Task.FromResult<FORM_FIELD_Master?>(null);

        var where = new WhereBuilder<FORM_FIELD_Master>()
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
    /// 取得 所有資料 給前端長樹
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<FormDesignerIndexViewModel> GetFormDesignerIndexViewModel(Guid? id, CancellationToken ct)
    {
        var master = await GetFormMasterAsync(id, ct) ?? new();

        var result = new FormDesignerIndexViewModel
        {
            FormHeader = master,
            BaseFields = null!,
            ViewFields = null!,
            FieldSetting = null!,
        };

        // 主表欄位
        var baseFields = GetFieldsByTableName(master.BASE_TABLE_NAME, master.BASE_TABLE_ID, TableSchemaQueryType.OnlyTable);
        baseFields.ID = master.ID;
        baseFields.SchemaQueryType = TableSchemaQueryType.OnlyTable;
        result.BaseFields = baseFields;

        // View 欄位
        var viewFields = GetFieldsByTableName(master.VIEW_TABLE_NAME, master.VIEW_TABLE_ID, TableSchemaQueryType.OnlyView);
        viewFields.ID = master.ID;
        viewFields.SchemaQueryType = TableSchemaQueryType.OnlyView;
        result.ViewFields = viewFields;

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
        // 白名單檢查：只允許英數、底線、點
        if (!string.IsNullOrWhiteSpace(tableName) &&
            !Regex.IsMatch(tableName, @"^[A-Za-z0-9_\.]+$"))
        {
            throw new ArgumentException("tableName 含非法字元");
        }

        // 判斷要查 VIEW 還是 TABLE
        var tableType = schemaType == TableSchemaQueryType.OnlyView ? "VIEW" : "BASE TABLE";

        string sql;
        object param;

        if (string.IsNullOrWhiteSpace(tableName))
        {
            // 沒輸入 tableName，就撈所有符合 schemaType 的表
            sql = @"SELECT TABLE_NAME 
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = @tableType;";
            param = new { tableType };
        }
        else
        {
            // 模糊搜尋
            sql = @"SELECT TABLE_NAME 
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME LIKE @tableName
                AND TABLE_TYPE = @tableType;";
            param = new { tableName = $"%{tableName}%", tableType };
        }

        return _con.Query<string>(sql, param).ToList();
    }
    
    public Guid GetOrCreateFormMasterId(FORM_FIELD_Master model)
    {
        var sql = @"SELECT ID FROM FORM_FIELD_Master WHERE ID = @id";
        var res = _con.QueryFirstOrDefault<Guid?>(sql, new { id = model.ID });

        if (res.HasValue)
            return res.Value;

        var insertId = model.ID == Guid.Empty ? Guid.NewGuid() : model.ID;
        _con.Execute(@"
        INSERT INTO FORM_FIELD_Master (ID, FORM_NAME, STATUS, SCHEMA_TYPE, BASE_TABLE_NAME, VIEW_TABLE_NAME, IS_DELETE)
        VALUES (@ID, @FORM_NAME, @STATUS, @SCHEMA_TYPE, @BASE_TABLE_NAME, @VIEW_TABLE_NAME, 0)", new
        {
            ID = insertId,
            model.FORM_NAME,
            model.STATUS,
            model.SCHEMA_TYPE,
            model.BASE_TABLE_NAME,
            model.VIEW_TABLE_NAME
        });

        return insertId;
    }

    /// <summary>
    /// 根據資料表名稱，取得所有欄位資訊並合併 欄位設定、驗證、語系資訊。
    /// </summary>
    /// <param name="tableName">使用者輸入的表名稱</param>
    /// <returns>回傳多筆 FormFieldViewModel</returns>
    public FormFieldListViewModel GetFieldsByTableName(string tableName, Guid? formMasterId, TableSchemaQueryType schemaType)
    {
        var columns = GetTableSchema(tableName);
        if (columns.Count == 0) return new();

        var configs = GetFieldConfigs(tableName, formMasterId);
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
            ID = masterId,
            TableName = tableName,
            Fields = fields,
            SchemaQueryType = schemaType
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
    public FormFieldViewModel? GetFieldById(Guid fieldId)
    {
        var cfg = _con.QueryFirstOrDefault<FormFieldConfigDto>(
            Sql.FieldConfigSelect + " WHERE ID = @fieldId", new { fieldId });
        if (cfg == null) return null;

        var master = _con.QueryFirst<FORM_FIELD_Master>(
            Sql.FormMasterById, new { id = cfg.FORM_FIELD_Master_ID });
        
        var pk = _schemaService.GetPrimaryKeyColumns(cfg.TABLE_NAME);

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
            // QUERY_CONDITION_SQL = cfg.QUERY_CONDITION_SQL ?? string.Empty,
            CAN_QUERY = cfg.CAN_QUERY,
            SchemaType = master.SCHEMA_TYPE
        };
    }

    /// <summary>
    /// 搜尋表格時，如設定檔不存在則先寫入預設欄位設定。
    /// </summary>
    /// <param name="tableName">資料表名稱</param>
    /// <returns>包含欄位設定的 ViewModel</returns>
    public FormFieldListViewModel? EnsureFieldsSaved(string tableName, Guid? formMasterId, TableSchemaQueryType schemaType)
    {
        var columns = GetTableSchema(tableName);

        if (columns.Count == 0) return null;

        var missingColumns = _requiredColumns
            .Where(req => !columns.Any(c => c.COLUMN_NAME.Equals(req, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missingColumns.Count > 0 && schemaType == TableSchemaQueryType.OnlyTable)
            throw new HttpStatusCodeException(
                HttpStatusCode.BadRequest,
                $"缺少必要欄位：{string.Join(", ", missingColumns)}");

        FORM_FIELD_Master model = new FORM_FIELD_Master
        {
            FORM_NAME = string.Empty,
            BASE_TABLE_NAME = schemaType == TableSchemaQueryType.OnlyView ? string.Empty : tableName,
            VIEW_TABLE_NAME = schemaType == TableSchemaQueryType.OnlyTable ? string.Empty : tableName,
            STATUS = (int)TableStatusType.Draft,
            SCHEMA_TYPE = schemaType
        };

        // 根據傳進來的formMasterId判斷為哪次操作的資料
        var configs = GetFieldConfigs(tableName, formMasterId);
        var masterId = formMasterId
                       ?? configs.Values.FirstOrDefault()?.FORM_FIELD_Master_ID
                       ?? GetOrCreateFormMasterId(model);


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
        var result = GetFieldsByTableName(tableName, masterId, schemaType);

        var master = _con.QueryFirst<FORM_FIELD_Master>(Sql.FormMasterById, new { id = masterId });
        result.formName = master.FORM_NAME;
        
        // 對於檢視表，先預設有下拉選單的設定，創建的ISUSESQL欄位會為NULL
        if (schemaType == TableSchemaQueryType.OnlyView)
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
    public void SetAllEditable(Guid formMasterId, string tableName, bool isEditable)
    {
        // Guid children = GetFormFieldMasterChildren(formMasterId);
        _con.Execute(Sql.SetAllEditable, new { formMasterId, tableName, isEditable });
    }

    /// <summary>
    /// 批次設定欄位的必填狀態，僅對可編輯欄位生效。
    /// </summary>
    public void SetAllRequired(Guid formMasterId, string tableName, bool isRequired)
    {
        // Guid children = GetFormFieldMasterChildren(formMasterId);
        _con.Execute(Sql.SetAllRequired, new { formMasterId, tableName, isRequired });
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
    public List<FormFieldValidationRuleDto> GetValidationRulesByFieldId(Guid fieldId)
    {
        var sql = Sql.ValidationRuleSelect + " WHERE FIELD_CONFIG_ID = @fieldId ORDER BY VALIDATION_ORDER";
        return _con.Query<FormFieldValidationRuleDto>(sql, new { fieldId }).ToList();
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
    /// <param name="model">驗證規則 DTO</param>
    public void InsertValidationRule(FormFieldValidationRuleDto model)
    {
        _con.Execute(Sql.InsertValidationRule, model);
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
    /// 儲存（更新）驗證規則。
    /// </summary>
    /// <param name="rule">要更新的驗證規則 DTO</param>
    /// <returns>更新成功則回傳 true</returns>
    public bool SaveValidationRule(FormFieldValidationRuleDto rule)
    {
       var res =_con.Execute(Sql.UpdateValidationRule, rule) > 0;
       return res;
    }

    /// <summary>
    /// 刪除一筆驗證規則。
    /// </summary>
    /// <param name="id">驗證規則的唯一識別碼</param>
    /// <returns>刪除成功則回傳 true</returns>
    public bool DeleteValidationRule(Guid id)
    {
        var res = _con.Execute(Sql.DeleteValidationRule, new { id }) > 0;
        return res;
    }

    public void EnsureDropdownCreated(Guid fieldId, bool? isUseSql = false, string? sql = null)
    {
        _con.Execute(Sql.EnsureDropdownExists, new { fieldId, isUseSql, sql });
    }
    
    public DropDownViewModel GetDropdownSetting(Guid fieldId)
    {
        var dropDown = _con.QueryFirstOrDefault<DropDownViewModel>(Sql.GetDropdownByFieldId, new { fieldId });

        if (dropDown == null)
        {
            return new DropDownViewModel();
        }
        var optionTexts = GetDropdownOptions(dropDown.ID);
        dropDown.OPTION_TEXT = optionTexts;

        return dropDown;
    }
    
    public List<FORM_FIELD_DROPDOWN_OPTIONS> GetDropdownOptions(Guid dropDownId)
    {
        var optionTexts = _con.Query<FORM_FIELD_DROPDOWN_OPTIONS>(Sql.GetOptionByDropdownId, new { dropDownId }).ToList();
        
        return optionTexts;
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

    public void DeleteDropdownOption(Guid optionId)
    {
        _con.Execute(Sql.DeleteDropdownOption, new { optionId });
    }
    
    public void SaveDropdownSql(Guid dropdownId, string sql)
    {
        _con.Execute(Sql.UpsertDropdownSql, new { dropdownId, sql });
    }
    
    public void SetDropdownMode(Guid dropdownId, bool isUseSql)
    {
        _con.Execute(Sql.SetDropdownMode, new { DropdownId = dropdownId, IsUseSql = isUseSql });
    }
    
    public ValidateSqlResultViewModel ValidateDropdownSql(string sql)
    {
        var result = new ValidateSqlResultViewModel();

        try
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                result.Success = false;
                result.Message = "SQL 不可為空。";
                return result;
            }

            if (Regex.IsMatch(sql, @"\b(insert|update|delete|drop|alter|truncate|exec|merge)\b", RegexOptions.IgnoreCase))
            {
                result.Success = false;
                result.Message = "僅允許查詢類 SQL。";
                return result;
            }

            var wasClosed = _con.State != System.Data.ConnectionState.Open;
            if (wasClosed) _con.Open();

            using var cmd = new SqlCommand(sql, _con);
            using var reader = cmd.ExecuteReader();

            var columns = reader.GetColumnSchema();
            if (columns.Count < 2)
            {
                result.Success = false;
                result.Message = "SQL 必須回傳至少兩個欄位，SELECT A AS ID, B AS NAME";
                return result;
            }

            // 檢查第一個欄位是否包含任一個 _excludeColumns 關鍵字
            if (!_excludeColumns.Any(ex =>
                    columns[0].ColumnName.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            {
                result.Success = false;
                result.Message = $"第一個欄位必須包含任一關鍵字：{string.Join(", ", _excludeColumns)}";
                return result;
            }

            var rows = new List<Dictionary<string, object>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[columns[i].ColumnName] = reader.GetValue(i);
                }
                rows.Add(row);
            }

            result.Success = true;
            result.RowCount = rows.Count;
            result.Rows = rows.Take(10).ToList(); // 最多回傳前 10 筆

            if (wasClosed) _con.Close();
        }
        catch (Exception ex)
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
        const string ColId   = "ID";
        const string ColName = "NAME";

        var validation = ValidateDropdownSql(sql);
        if (!validation.Success)
            return validation;

        // 解析來源表名：先用較穩健的 regex（容許 [dbo].[Table] / dbo.Table / Table）
        var optionTable = TryExtractTableName(sql);
        if (string.IsNullOrWhiteSpace(optionTable))
        {
            return Fail("無法解析來源表名稱（請使用單一 FROM，並避免子查詢/CTE/多表 JOIN）。");
        }

        var wasClosed = _con.State != System.Data.ConnectionState.Open;
        if (wasClosed) _con.Open();

        using var tx = _con.BeginTransaction();
        try
        {
            // 要求 SQL 結果一定有兩個別名：ID、NAME
            var rows = _con.Query<DropdownOptionRow>(sql, transaction: tx);

            int i = 0;
            foreach (var r in rows)
            {
                i++;

                // 明確檢查 NULL 與空白
                if (r.ID is null)
                {
                    tx.Rollback();
                    return Fail($"第 {i} 筆資料的 {ColId} 為 NULL，請修正來源 SQL 或清理資料。");
                }
                if (r.NAME is null)
                {
                    tx.Rollback();
                    return Fail($"第 {i} 筆資料的 {ColName} 為 NULL，請修正來源 SQL 或清理資料。");
                }

                var optionValue = r.ID.Trim();
                var optionText  = r.NAME.Trim();

                if (optionValue.Length == 0)
                {
                    tx.Rollback();
                    return Fail($"第 {i} 筆資料的 {ColId} 為空字串。");
                }
                if (optionText.Length == 0)
                {
                    tx.Rollback();
                    return Fail($"第 {i} 筆資料的 {ColName} 為空字串。");
                }

                // 參數化寫入（忽略重複）
                _con.Execute(Sql.InsertOptionIgnoreDuplicate, new
                {
                    DropdownId  = dropdownId,
                    OptionTable = optionTable,
                    OptionValue = optionValue,
                    OptionText  = optionText
                }, transaction: tx);
            }

            tx.Commit();
            return new ValidateSqlResultViewModel
            {
                Success = true,
                Message = "匯入完成。"
            };
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Fail($"匯入失敗：{ex.Message}");
        }
        finally
        {
            if (wasClosed) _con.Close();
        }

        // local helpers
        static string? TryExtractTableName(string sql)
        {
            // 支援 [dbo].[X]、dbo.X、[X]、X，取實際表名那段
            var m = Regex.Match(sql,
                @"from\s+(?:\[(?<schema>[^\]]+)\]\.)?\[?(?<table>[A-Za-z0-9_]+)\]?",
                RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["table"].Value : null;
        }

        static ValidateSqlResultViewModel Fail(string msg) => new()
        {
            Success = false,
            Message = msg
        };
    }

    /// <summary>
    /// 強型別承接查詢結果，避免 dynamic 與魔法字串
    /// </summary>
    public sealed class DropdownOptionRow
    {
        // 欄位名需匹配 SQL 別名：SELECT A AS ID, B AS NAME
        public string? ID   { get; set; }
        public string? NAME { get; set; }
    }
    
    public Guid SaveFormHeader( FormHeaderViewModel model )
    {
        var baseField = _con.QueryFirst<FORM_FIELD_Master>(
            Sql.FormMasterById, new { id = model.BASE_TABLE_ID }) ?? throw new InvalidOperationException("主表查無資料");
        var viewField = _con.QueryFirst<FORM_FIELD_Master>(
            Sql.FormMasterById, new { id = model.VIEW_TABLE_ID }) ?? throw new InvalidOperationException("檢視表查無資料");

        var baseTableName = baseField.BASE_TABLE_NAME;
        var viewTableName = viewField.VIEW_TABLE_NAME;
        
        // 確保主表與顯示用 View 皆能成功查詢，避免儲存無效設定
        if (GetTableSchema(baseTableName).Count == 0)
            throw new InvalidOperationException("主表名稱查無資料");

        if (GetTableSchema(viewTableName).Count == 0)
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
            SCHEMA_TYPE = TableSchemaQueryType.All
        });
        return id;
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
    private Dictionary<string, FormFieldConfigDto> GetFieldConfigs(string tableName, Guid? formMasterId)
    {
        var sql = Sql.FieldConfigSelect + " WHERE TABLE_NAME = @TableName AND FORM_FIELD_Master_ID = @FormMasterId";
        var res = _con.Query<FormFieldConfigDto>(sql, new { TableName = tableName, FormMasterId = formMasterId })
            .ToDictionary(x => x.COLUMN_NAME, StringComparer.OrdinalIgnoreCase);
        return res;
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
        
        public const string FieldConfigSelect = @"/**/
SELECT *
FROM FORM_FIELD_CONFIG";

        public const string ValidationRuleSelect = @"/**/
SELECT *
FROM FORM_FIELD_VALIDATION_RULE";

        // 20250814，主檔可以和主檔本身自我關聯
        public const string TableSchemaSelect = @"/**/
SELECT COLUMN_NAME, DATA_TYPE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName
ORDER BY ORDINAL_POSITION";
        
//         public const string TableSchemaSelect = @"/**/
// SELECT COLUMN_NAME, DATA_TYPE, ORDINAL_POSITION
// FROM INFORMATION_SCHEMA.COLUMNS
// WHERE TABLE_NAME = @TableName
//   AND (
//       (@Type = 0 AND TABLE_NAME NOT LIKE 'V_%')
//       OR (@Type = 1 AND TABLE_NAME LIKE 'V_%')
//   )
// ORDER BY ORDINAL_POSITION";

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
        VIEW_TABLE_ID    = @VIEW_TABLE_ID
WHEN NOT MATCHED THEN
    INSERT (
        ID, FORM_NAME, BASE_TABLE_NAME, VIEW_TABLE_NAME,
        BASE_TABLE_ID, VIEW_TABLE_ID, STATUS, SCHEMA_TYPE, IS_DELETE)
    VALUES (
        @ID, @FORM_NAME, @BASE_TABLE_NAME, @VIEW_TABLE_NAME,
        @BASE_TABLE_ID, @VIEW_TABLE_ID, @STATUS, @SCHEMA_TYPE, 0)
OUTPUT INSERTED.ID;";

        public const string CheckFormMasterExists = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_Master
WHERE BASE_TABLE_ID = @baseTableId
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
        CONTROL_TYPE, IS_REQUIRED, IS_EDITABLE, QUERY_DEFAULT_VALUE, FIELD_ORDER, QUERY_COMPONENT, QUERY_CONDITION, CAN_QUERY, CREATE_TIME
    )
    VALUES (
        @ID, @FORM_FIELD_Master_ID, @TABLE_NAME, @COLUMN_NAME, @DATA_TYPE,
        @CONTROL_TYPE, @IS_REQUIRED, @IS_EDITABLE, @QUERY_DEFAULT_VALUE, @FIELD_ORDER, @QUERY_COMPONENT, @QUERY_CONDITION, @CAN_QUERY, GETDATE()
    );";

        public const string CheckFieldExists         = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_CONFIG WHERE ID = @fieldId";

        public const string SetAllEditable = @"/**/
UPDATE FORM_FIELD_CONFIG
SET IS_EDITABLE = @isEditable
WHERE FORM_FIELD_Master_ID = @formMasterId AND TABLE_NAME = @tableName;

-- 若不可編輯，強制取消必填
IF (@isEditable = 0)
BEGIN
    UPDATE FORM_FIELD_CONFIG
    SET IS_REQUIRED = 0
    WHERE FORM_FIELD_Master_ID = @formMasterId AND TABLE_NAME = @tableName;
END
";

        public const string SetAllRequired = @"/**/
UPDATE FORM_FIELD_CONFIG
SET IS_REQUIRED = CASE WHEN @isRequired = 1 AND IS_EDITABLE = 1 THEN 1 ELSE 0 END
WHERE FORM_FIELD_Master_ID = @formMasterId AND TABLE_NAME = @tableName";
        
        public const string CountValidationRules     = @"/**/
SELECT COUNT(1) FROM FORM_FIELD_VALIDATION_RULE WHERE FIELD_CONFIG_ID = @fieldId";
        
        public const string InsertValidationRule     = @"/**/
INSERT INTO FORM_FIELD_VALIDATION_RULE (
    ID, FIELD_CONFIG_ID, VALIDATION_TYPE, VALIDATION_VALUE,
    MESSAGE_ZH, MESSAGE_EN, VALIDATION_ORDER
) VALUES (
    @ID, @FIELD_CONFIG_ID, @VALIDATION_TYPE, @VALIDATION_VALUE,
    @MESSAGE_ZH, @MESSAGE_EN, @VALIDATION_ORDER
)";

        public const string GetNextValidationOrder   = @"/**/
SELECT ISNULL(MAX(VALIDATION_ORDER), 0) + 1 FROM FORM_FIELD_VALIDATION_RULE WHERE FIELD_CONFIG_ID = @fieldId";
        
        public const string GetControlTypeByFieldId  = @"/**/
SELECT CONTROL_TYPE FROM FORM_FIELD_CONFIG WHERE ID = @fieldId";
        
        public const string UpdateValidationRule     = @"/**/
UPDATE FORM_FIELD_VALIDATION_RULE SET
    VALIDATION_TYPE  = @VALIDATION_TYPE,
    VALIDATION_VALUE = @VALIDATION_VALUE,
    MESSAGE_ZH       = @MESSAGE_ZH,
    MESSAGE_EN       = @MESSAGE_EN,
    EDIT_TIME        = GETDATE()
WHERE ID = @ID";
        public const string DeleteValidationRule     = @"/**/
DELETE FROM FORM_FIELD_VALIDATION_RULE WHERE ID = @id";

        public const string GetRequiredFieldIds      = @"/**/
SELECT FIELD_CONFIG_ID FROM FORM_FIELD_VALIDATION_RULE";

        public const string EnsureDropdownExists = @"
/* 僅在尚未存在時插入 dropdown 主檔 */
IF NOT EXISTS (
    SELECT 1 FROM FORM_FIELD_DROPDOWN WHERE FORM_FIELD_CONFIG_ID = @fieldId
)
BEGIN
    INSERT INTO FORM_FIELD_DROPDOWN (ID, FORM_FIELD_CONFIG_ID, ISUSESQL, DROPDOWNSQL)
    VALUES (NEWID(), @fieldId, @isUseSql, @sql)
END
";
        
        public const string GetDropdownByFieldId = @"/**/
SELECT * FROM FORM_FIELD_DROPDOWN WHERE FORM_FIELD_CONFIG_ID = @fieldId";
        
        public const string GetOptionByDropdownId = @"/**/
SELECT * FROM FORM_FIELD_DROPDOWN_OPTIONS WHERE FORM_FIELD_DROPDOWN_ID = @dropDownId AND OPTION_TABLE IS NULL --NULL的是使用者自訂
";

        public const string UpsertDropdownSql = @"/**/
UPDATE FORM_FIELD_DROPDOWN
  SET DROPDOWNSQL = @sql, ISUSESQL = 1
  WHERE ID = @dropdownId;
";
        
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
        OPTION_TABLE = source.OPTION_TABLE
WHEN NOT MATCHED THEN
    INSERT (ID, FORM_FIELD_DROPDOWN_ID, OPTION_TEXT, OPTION_VALUE, OPTION_TABLE)
    VALUES (ISNULL(source.ID, NEWID()),       -- 若 Guid.Empty → 直接 NEWID()
            source.FORM_FIELD_DROPDOWN_ID,
            source.OPTION_TEXT,
            source.OPTION_VALUE,
            source.OPTION_TABLE)
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
    INSERT (ID, FORM_FIELD_DROPDOWN_ID, OPTION_TABLE, OPTION_VALUE, OPTION_TEXT)
    VALUES (NEWID(), src.FORM_FIELD_DROPDOWN_ID, src.OPTION_TABLE, src.OPTION_VALUE, src.OPTION_TEXT);
";

        public const string DeleteDropdownOption = @"/**/
DELETE FROM FORM_FIELD_DROPDOWN_OPTIONS WHERE ID = @optionId;
";
        
        public const string SetDropdownMode = @"
UPDATE dbo.FORM_FIELD_DROPDOWN
SET ISUSESQL   = @IsUseSql
WHERE ID = @DropdownId;
";

        public const string FormMasterSelect = @"SELECT * FROM FORM_FIELD_Master WHERE STATUS IN @STATUS";
        public const string FormMasterById   = @"SELECT * FROM FORM_FIELD_Master WHERE ID = @id";
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