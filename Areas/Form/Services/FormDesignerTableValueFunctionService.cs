using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Options;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Services.CurrentUser.Interfaces;
using DcMateH5Api.SqlHelper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace DcMateH5Api.Areas.Form.Services;

public class FormDesignerTableValueFunctionService : IFormDesignerTableValueFunctionService
{
    private readonly SqlConnection _con;
    private readonly IConfiguration _configuration;
    private readonly ISchemaService _schemaService;
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IFormDesignerService _formDesignerService;
    private readonly IFormFieldMasterService _formFieldMasterService;
    private readonly IReadOnlyList<string> _relationColumnSuffixes;

    public FormDesignerTableValueFunctionService(
        SQLGenerateHelper sqlHelper,
        SqlConnection connection,
        IConfiguration configuration,
        ISchemaService schemaService,
        IFormFieldMasterService formFieldMasterService,
        IFormDesignerService formDesignerService,
        IOptions<FormSettings> formSettings,
        ICurrentUserAccessor currentUser)
    {
        _con = connection;
        _configuration = configuration;
        _schemaService = schemaService;
        _sqlHelper = sqlHelper;
        _formFieldMasterService = formFieldMasterService;
        _formDesignerService = formDesignerService;
        _currentUser = currentUser;
        
        var resolvedSettings = formSettings?.Value ?? new FormSettings();
        _relationColumnSuffixes = resolvedSettings.GetRelationColumnSuffixesOrDefault();
    }
    
    private Guid GetCurrentUserId()
    {
        var user = _currentUser.Get();
        return user.Id;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="tvpName"></param>
    /// <param name="formMasterId"></param>
    /// <param name="schemaType"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<FormFieldListViewModel?> EnsureFieldsSaved( string tvpName, Guid? formMasterId, TableSchemaQueryType schemaType, CancellationToken ct )
    {
        ct.ThrowIfCancellationRequested();

        return await _sqlHelper.TxAsync(async (conn, tx, ct) =>
        {
            // 交易內使用同一個 conn/tx，確保整段欄位同步的一致性與原子性
            var columns = await GetObjectSchemaInTxAsync(conn, tx, "dbo", tvpName, ct);
            if (columns.Count == 0) return null;

            var masterId = await _formDesignerService.ResolveMasterIdAsync(conn, tx, tvpName, formMasterId, schemaType, ct);

            var configs = await _formDesignerService.GetFieldConfigsInTxAsync(conn, tx, tvpName, masterId, ct).ConfigureAwait(false);
            await _formDesignerService.UpsertMissingConfigsInTxAsync(conn, tx, tvpName, masterId, schemaType, columns, configs, ct);
            
            // 回傳結果同樣走交易內查詢，避免交易外查詢讀到不一致資料
            return await GetFieldsByTableNameInTxAsync(conn, tx, tvpName, masterId, schemaType, ct);
        }, ct: ct);
    }
    
    public async Task<Guid> SaveTableValueFunctionFormHeader(FormHeaderTableValueFunctionViewModel model, CancellationToken ct)
    {
        var whereTvf = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, model.TVF_TABLE_ID)
            .AndNotDeleted();
        
        var tvfMaster = await _sqlHelper.SelectFirstOrDefaultAsync(whereTvf)
                        ?? throw new InvalidOperationException("TVF 查無資料");

        var tvfTableName = tvfMaster.TVP_TABLE_NAME;
        
        // 執行更新
        var affectedRows = await _con.ExecuteAsync(Sql.UpdateFormMaster, new
        {
            model.ID, 
            model.FORM_NAME,
            model.FORM_CODE,
            model.FORM_DESCRIPTION,
            
            model.TVF_TABLE_ID,
            TVP_TABLE_NAME = tvfTableName,
            
            STATUS = (int)TableStatusType.Active,
            SCHEMA_TYPE = TableSchemaQueryType.All,
            FUNCTION_TYPE = FormFunctionType.TableValueFunctionMaintenance,

            // UPDATE 只需要更新編輯時間與編輯者
            EDIT_TIME = DateTime.Now,
            EDIT_USER = GetCurrentUserId()
        });

        // 如果受影響行數為 0，代表 ID 不存在
        if (affectedRows == 0)
        {
            throw new Exception($"更新失敗：找不到 ID 為 {model.ID} 的資料。");
        }

        return model.ID;
    }
    
    /// <summary>
    /// 從 SQL Server 取得指定物件（Table/View/TVF）的欄位結構（交易內）。
    /// </summary>
    private async Task<List<DbColumnInfo>> GetObjectSchemaInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        string schemaName,
        string objectName,
        CancellationToken ct)
    {
        var columns = await conn.QueryAsync<DbColumnInfo>(new CommandDefinition(
            Sql.ObjectSchemaAndTvfInputsSelect,
            new { SchemaName = schemaName, ObjectName = objectName },
            transaction: tx,
            cancellationToken: ct));

        return columns.ToList();
    }

    private async Task<FormFieldListViewModel> GetFieldsByTableNameInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        string tvpName,
        Guid? formMasterId,
        TableSchemaQueryType schemaType,
        CancellationToken ct)
    {
        var columns = await GetObjectSchemaInTxAsync(conn, tx, "dbo", tvpName, ct);
        if (columns.Count == 0) return new();

        var configs = await _formDesignerService.GetFieldConfigsInTxAsync(conn, tx, tvpName, formMasterId, ct).ConfigureAwait(false);

        var masterId = formMasterId ?? configs.Values.First().FORM_FIELD_MASTER_ID;

        var fields = new List<FormFieldViewModel>(columns.Count);
        foreach (var col in columns)
        {
            var columnName = col.COLUMN_NAME;
            var dataType = col.DATA_TYPE;
            var isTvfQueryParameter = col.isTvfQueryParameter;
            var isNullable = col.SourceIsNullable;

            var hasCfg = configs.TryGetValue(columnName, out var cfg);
            var fieldId = hasCfg ? cfg!.ID : Guid.NewGuid();

            var vm = new FormFieldViewModel
            {
                ID = fieldId,
                FORM_FIELD_MASTER_ID = masterId,
                FORM_FIELD_DROPDOWN_ID = null,
                TableName = tvpName,
                IsNullable = isNullable,
                COLUMN_NAME = columnName,
                IS_TVF_QUERY_PARAMETER = isTvfQueryParameter,
                DISPLAY_NAME = cfg?.DISPLAY_NAME ?? columnName,
                DATA_TYPE = dataType,
                CONTROL_TYPE = cfg?.CONTROL_TYPE,
                CONTROL_TYPE_WHITELIST = null,
                QUERY_COMPONENT_TYPE_WHITELIST = null,
                IS_REQUIRED = cfg?.IS_REQUIRED ?? false,
                IS_EDITABLE = cfg?.IS_EDITABLE ?? true,
                IS_DISPLAYED = cfg?.IS_DISPLAYED ?? true,
                IS_VALIDATION_RULE = null,
                IS_PK = false,
                QUERY_DEFAULT_VALUE = cfg?.QUERY_DEFAULT_VALUE,
                SchemaType = schemaType,
                QUERY_COMPONENT = cfg?.QUERY_COMPONENT ?? QueryComponentType.None,
                QUERY_CONDITION = cfg?.QUERY_CONDITION,
                CAN_QUERY = cfg?.CAN_QUERY ?? false,
                FIELD_ORDER = cfg?.FIELD_ORDER,
                DETAIL_TO_RELATION_DEFAULT_COLUMN = cfg?.DETAIL_TO_RELATION_DEFAULT_COLUMN
            };

            _formDesignerService.ApplySchemaPolicy(vm, schemaType);
            fields.Add(vm);
        }

        return new FormFieldListViewModel
        {
            Fields = fields.OrderBy(f => f.FIELD_ORDER).ToList()
        };
    }
    
    private static class Sql
    {
        public const string ObjectSchemaAndTvfInputsSelect = @"
/**/
-- (1) TVF input parameters
SELECT
    p.name AS COLUMN_NAME,
    t.name AS DATA_TYPE,
    p.parameter_id AS ORDINAL_POSITION,
    'YES' AS IS_NULLABLE,
    CONVERT(bit, 1) AS isTvfQueryParameter
FROM sys.objects o
JOIN sys.parameters p ON o.object_id = p.object_id
JOIN sys.types t ON p.user_type_id = t.user_type_id
WHERE o.name = @ObjectName
  AND SCHEMA_NAME(o.schema_id) = @SchemaName
  AND o.type IN ('IF','TF')

UNION ALL

-- (2) Returned table columns (Table/View/TVF)
SELECT
    c.name AS COLUMN_NAME,
    t.name AS DATA_TYPE,
    c.column_id AS ORDINAL_POSITION,
    CASE WHEN c.is_nullable = 1 THEN 'YES' ELSE 'NO' END AS IS_NULLABLE,
    CONVERT(bit, 0) AS isTvfQueryParameter
FROM sys.objects o
JOIN sys.columns c ON o.object_id = c.object_id
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE o.name = @ObjectName
  AND SCHEMA_NAME(o.schema_id) = @SchemaName
  AND o.type IN ('U','V','IF','TF')

ORDER BY isTvfQueryParameter DESC, ORDINAL_POSITION;
";
        
        public const string UpdateFormMaster = @"
UPDATE FORM_FIELD_MASTER
SET
    FORM_NAME        = @FORM_NAME,
    FORM_CODE        = @FORM_CODE,
    FORM_DESCRIPTION = @FORM_DESCRIPTION,
    STATUS           = @STATUS,
    SCHEMA_TYPE      = @SCHEMA_TYPE,
    FUNCTION_TYPE    = @FUNCTION_TYPE,
    EDIT_TIME        = @EDIT_TIME,
    EDIT_USER        = @EDIT_USER
WHERE ID = @ID;";
    }
}