using System.Data;
using System.Net;
using System.Text.RegularExpressions;
using Dapper;
using DcMateClassLibrary.Enums.Form;
using DcMateClassLibrary.Helper.FormHelper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Form.Form;
using DcMateH5.Abstractions.Form.FormLogic;
using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Abstractions.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DcMateH5.Infrastructure.Form.Form;

public class FormViewDesignerService : IFormViewDesignerService
{
    private const FormFunctionType FunctionType = FormFunctionType.ViewQueryMaintenance;
    private const TableSchemaQueryType SchemaType = TableSchemaQueryType.OnlyView;

    private static readonly Regex SafeObjectNameRegex =
        new("^[A-Za-z0-9_]+(\\.[A-Za-z0-9_]+)?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly SqlConnection _con;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IFormDesignerService _formDesignerService;
    private readonly IFormFieldMasterService _formFieldMasterService;
    private readonly ISchemaService _schemaService;

    public FormViewDesignerService(
        SqlConnection connection,
        ICurrentUserAccessor currentUser,
        IFormDesignerService formDesignerService,
        IFormFieldMasterService formFieldMasterService,
        ISchemaService schemaService)
    {
        _con = connection;
        _currentUser = currentUser;
        _formDesignerService = formDesignerService;
        _formFieldMasterService = formFieldMasterService;
        _schemaService = schemaService;
    }

    public Task<List<FormFieldMasterDto>> GetFormMasters(string? q, CancellationToken ct = default)
        => _formDesignerService.GetFormMasters(FunctionType, q, ct);

    public async Task<FormDesignerIndexViewModel> GetDesigner(Guid id, CancellationToken ct = default)
    {
        var header = await GetViewHeaderAsync(id, ct);

        if (header.VIEW_TABLE_ID == null || string.IsNullOrWhiteSpace(header.VIEW_TABLE_NAME))
        {
            throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "View 主檔缺少 VIEW_TABLE 設定。");
        }

        var viewFields = await GetFieldsByViewName(header.VIEW_TABLE_NAME, header.VIEW_TABLE_ID.Value, ct);

        return new FormDesignerIndexViewModel
        {
            FormHeader = header,
            ViewFields = viewFields,
            BaseFields = new FormFieldListViewModel(),
            DetailFields = new FormFieldListViewModel(),
            ViewDetailFields = new FormFieldListViewModel(),
            MappingFields = new FormFieldListViewModel(),
            TvfFields = new FormFieldListViewModel()
        };
    }

    public List<string> SearchViews(string? viewName)
        => _formDesignerService.SearchTables(viewName, TableQueryType.OnlyViewTable);

    public Task<FormFieldViewModel?> GetFieldById(Guid fieldId)
        => _formDesignerService.GetFieldById(fieldId);

    public async Task<FormFieldListViewModel?> EnsureFieldsSaved(string viewName, Guid? formMasterId, CancellationToken ct = default)
    {
        var (schemaName, objectName) = ParseObjectName(viewName);

        return await WithTransactionAsync(async (conn, tx) =>
        {
            var columns = await _schemaService.GetObjectSchemaInTxAsync(conn, tx, schemaName, objectName, ct);
            if (columns.Count == 0)
            {
                return null;
            }

            var masterId = await _formDesignerService.ResolveMasterIdAsync(conn, tx, viewName, formMasterId, SchemaType, ct);
            var configs = await _formDesignerService.GetFieldConfigsInTxAsync(conn, tx, viewName, masterId, ct);
            await _formDesignerService.UpsertMissingConfigsInTxAsync(conn, tx, viewName, masterId, SchemaType, columns, configs, ct);

            return await BuildFieldsInTxAsync(conn, tx, viewName, masterId, schemaName, objectName, columns, ct);
        }, ct);
    }

    public async Task<FormFieldListViewModel> GetFieldsByViewName(string viewName, Guid formMasterId, CancellationToken ct = default)
    {
        var fields = await EnsureFieldsSaved(viewName, formMasterId, ct);
        return fields ?? new FormFieldListViewModel();
    }

    public Task MoveFieldAsync(MoveFormFieldRequest req, CancellationToken ct = default)
        => _formDesignerService.MoveFieldAsync(req, ct);

    public Task UpdateFormName(UpdateFormNameViewModel model, CancellationToken ct = default)
        => _formDesignerService.UpdateFormName(model, ct);

    public Task Delete(Guid id, CancellationToken ct = default)
        => _formDesignerService.DeleteFormMaster(id, ct);

    public Task UpsertFieldAsync(FormFieldViewModel model, CancellationToken ct = default)
    {
        model.SchemaType = SchemaType;
        model.IS_EDITABLE = false;
        model.IS_REQUIRED = false;
        return _formDesignerService.UpsertFieldAsync(model, model.FORM_FIELD_MASTER_ID, ct);
    }

    public async Task<Guid> SaveViewFormHeader(FormViewHeaderViewModel model, CancellationToken ct = default)
    {
        if (model.VIEW_TABLE_ID == Guid.Empty)
        {
            throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "VIEW_TABLE_ID 不可為空");
        }

        if (model.ID != Guid.Empty)
        {
            var existingMaster = await _formFieldMasterService.GetFormFieldMasterFromIdAsync(model.ID, ct);
            if (existingMaster != null && existingMaster.FUNCTION_TYPE != FunctionType)
            {
                throw new HttpStatusCodeException(HttpStatusCode.Conflict, "ID 已被其他表單模組使用，不能直接覆蓋。");
            }
        }

        var viewMaster = await _formFieldMasterService.GetFormFieldMasterFromIdAsync(model.VIEW_TABLE_ID, ct);
        if (viewMaster == null || viewMaster.SCHEMA_TYPE != SchemaType || string.IsNullOrWhiteSpace(viewMaster.VIEW_TABLE_NAME))
        {
            throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "指定的 VIEW_TABLE_ID 並非有效的 View 物件。");
        }

        var duplicateCount = await _con.ExecuteScalarAsync<int>(new CommandDefinition(
            Sql.CountDuplicateViewHeaders,
            new
            {
                Id = model.ID,
                ViewTableId = model.VIEW_TABLE_ID,
                FunctionType = (int)FunctionType
            },
            cancellationToken: ct));

        if (duplicateCount > 0)
        {
            throw new HttpStatusCodeException(HttpStatusCode.Conflict, "相同的 VIEW_TABLE_ID 已存在 View 查詢設定。");
        }

        var currentUserId = GetCurrentUserId();
        var now = DateTime.Now;
        var id = model.ID == Guid.Empty ? Guid.NewGuid() : model.ID;

        return await _con.ExecuteScalarAsync<Guid>(new CommandDefinition(
            Sql.UpsertViewHeader,
            new
            {
                ID = id,
                model.FORM_NAME,
                model.FORM_CODE,
                model.FORM_DESCRIPTION,
                VIEW_TABLE_ID = model.VIEW_TABLE_ID,
                VIEW_TABLE_NAME = viewMaster.VIEW_TABLE_NAME,
                STATUS = (int)TableStatusType.Active,
                SCHEMA_TYPE = (int)TableSchemaQueryType.All,
                FUNCTION_TYPE = (int)FunctionType,
                CREATE_USER = currentUserId,
                CREATE_TIME = now,
                EDIT_USER = currentUserId,
                EDIT_TIME = now
            },
            cancellationToken: ct));
    }

    private async Task<FormFieldListViewModel> BuildFieldsInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        string viewName,
        Guid masterId,
        string schemaName,
        string objectName,
        IReadOnlyList<DbColumnInfo> columns,
        CancellationToken ct)
    {
        var columnMap = columns.ToDictionary(column => column.COLUMN_NAME, StringComparer.OrdinalIgnoreCase);
        var configs = await _formDesignerService.GetFieldConfigsInTxAsync(conn, tx, viewName, masterId, ct);
        var dropdownMap = await LoadDropdownMapAsync(conn, tx, masterId, ct);
        var primaryKeys = await LoadPrimaryKeysAsync(conn, tx, schemaName, objectName, ct);

        var fields = new List<FormFieldViewModel>();

        foreach (var config in configs.Values.OrderBy(config => config.FIELD_ORDER))
        {
            if (!columnMap.TryGetValue(config.COLUMN_NAME, out var column))
            {
                continue;
            }

            dropdownMap.TryGetValue(config.ID, out var dropdownId);

            var field = new FormFieldViewModel
            {
                ID = config.ID,
                FORM_FIELD_MASTER_ID = masterId,
                FORM_FIELD_DROPDOWN_ID = dropdownId == Guid.Empty ? null : dropdownId,
                TableName = viewName,
                IsNullable = column.SourceIsNullable,
                COLUMN_NAME = config.COLUMN_NAME,
                DISPLAY_NAME = config.DISPLAY_NAME,
                DATA_TYPE = column.DATA_TYPE,
                CONTROL_TYPE = config.CONTROL_TYPE,
                CONTROL_TYPE_WHITELIST = FormFieldHelper.GetControlTypeWhitelist(column.DATA_TYPE),
                QUERY_COMPONENT_TYPE_WHITELIST = FormFieldHelper.GetQueryConditionTypeWhitelist(column.DATA_TYPE),
                IS_REQUIRED = config.IS_REQUIRED,
                IS_EDITABLE = config.IS_EDITABLE,
                IS_DISPLAYED = config.IS_DISPLAYED,
                IS_VALIDATION_RULE = false,
                IS_PK = primaryKeys.Contains(config.COLUMN_NAME),
                FIELD_ORDER = config.FIELD_ORDER,
                QUERY_DEFAULT_VALUE = config.QUERY_DEFAULT_VALUE,
                QUERY_COMPONENT = config.QUERY_COMPONENT,
                QUERY_CONDITION = config.QUERY_CONDITION,
                CAN_QUERY = config.CAN_QUERY,
                SchemaType = SchemaType
            };

            _formDesignerService.ApplySchemaPolicy(field, SchemaType);
            fields.Add(field);
        }

        return new FormFieldListViewModel { Fields = fields };
    }

    private async Task<Dictionary<Guid, Guid>> LoadDropdownMapAsync(SqlConnection conn, SqlTransaction tx, Guid masterId, CancellationToken ct)
    {
        var rows = await conn.QueryAsync<(Guid FieldId, Guid DropdownId)>(new CommandDefinition(
            Sql.GetDropdownMap,
            new { MasterId = masterId },
            transaction: tx,
            cancellationToken: ct));

        return rows.ToDictionary(row => row.FieldId, row => row.DropdownId);
    }

    private async Task<HashSet<string>> LoadPrimaryKeysAsync(SqlConnection conn, SqlTransaction tx, string schemaName, string objectName, CancellationToken ct)
    {
        var rows = await conn.QueryAsync<string>(new CommandDefinition(
            Sql.GetPrimaryKeys,
            new { SchemaName = schemaName, ObjectName = objectName },
            transaction: tx,
            cancellationToken: ct));

        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<FormFieldMasterDto> GetViewHeaderAsync(Guid id, CancellationToken ct)
    {
        var header = await _con.QueryFirstOrDefaultAsync<FormFieldMasterDto>(new CommandDefinition(
            Sql.GetViewHeaderById,
            new { Id = id, FunctionType = (int)FunctionType },
            cancellationToken: ct));

        if (header == null)
        {
            throw new HttpStatusCodeException(HttpStatusCode.NotFound, "查無 View 查詢主檔。");
        }

        return header;
    }

    private Guid GetCurrentUserId() => _currentUser.Get().Id;

    private static (string SchemaName, string ObjectName) ParseObjectName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName) || !SafeObjectNameRegex.IsMatch(fullName))
        {
            throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "View 名稱格式錯誤。");
        }

        var parts = fullName.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : ("dbo", parts[0]);
    }

    private async Task<T> WithTransactionAsync<T>(
        Func<SqlConnection, SqlTransaction, Task<T>> action,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var shouldClose = _con.State != ConnectionState.Open;
        if (shouldClose)
        {
            await _con.OpenAsync(ct);
        }

        await using var tx = (SqlTransaction)await _con.BeginTransactionAsync(ct);
        try
        {
            var result = await action(_con, tx);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (shouldClose)
            {
                await _con.CloseAsync();
            }
        }
    }

    private static class Sql
    {
        public const string GetViewHeaderById = @"
SELECT TOP (1) *
FROM FORM_FIELD_MASTER
WHERE ID = @Id
  AND FUNCTION_TYPE = @FunctionType
  AND IS_DELETE = 0;";

        public const string GetDropdownMap = @"
SELECT
    c.ID AS FieldId,
    d.ID AS DropdownId
FROM FORM_FIELD_CONFIG c
LEFT JOIN FORM_FIELD_DROPDOWN d
    ON d.FORM_FIELD_CONFIG_ID = c.ID
   AND d.IS_DELETE = 0
WHERE c.FORM_FIELD_MASTER_ID = @MasterId
  AND c.IS_DELETE = 0;";

        public const string GetPrimaryKeys = @"
SELECT KU.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
 AND TC.TABLE_SCHEMA = KU.TABLE_SCHEMA
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND TC.TABLE_SCHEMA = @SchemaName
  AND TC.TABLE_NAME = @ObjectName
ORDER BY KU.ORDINAL_POSITION;";

        public const string CountDuplicateViewHeaders = @"
SELECT COUNT(1)
FROM FORM_FIELD_MASTER
WHERE VIEW_TABLE_ID = @ViewTableId
  AND FUNCTION_TYPE = @FunctionType
  AND IS_DELETE = 0
  AND (@Id = CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier) OR ID <> @Id);";

        public const string UpsertViewHeader = @"
MERGE FORM_FIELD_MASTER AS target
USING (SELECT @ID AS ID) AS src
ON target.ID = src.ID

WHEN MATCHED THEN
    UPDATE SET
        FORM_NAME = @FORM_NAME,
        FORM_CODE = @FORM_CODE,
        FORM_DESCRIPTION = @FORM_DESCRIPTION,
        BASE_TABLE_NAME = NULL,
        DETAIL_TABLE_NAME = NULL,
        VIEW_TABLE_NAME = @VIEW_TABLE_NAME,
        MAPPING_TABLE_NAME = NULL,
        TVF_TABLE_NAME = NULL,
        BASE_TABLE_ID = NULL,
        DETAIL_TABLE_ID = NULL,
        VIEW_TABLE_ID = @VIEW_TABLE_ID,
        MAPPING_TABLE_ID = NULL,
        TVF_TABLE_ID = NULL,
        STATUS = @STATUS,
        SCHEMA_TYPE = @SCHEMA_TYPE,
        FUNCTION_TYPE = @FUNCTION_TYPE,
        EDIT_USER = @EDIT_USER,
        EDIT_TIME = @EDIT_TIME

WHEN NOT MATCHED THEN
    INSERT (
        ID,
        FORM_NAME,
        FORM_CODE,
        FORM_DESCRIPTION,
        BASE_TABLE_NAME,
        DETAIL_TABLE_NAME,
        VIEW_TABLE_NAME,
        MAPPING_TABLE_NAME,
        TVF_TABLE_NAME,
        BASE_TABLE_ID,
        DETAIL_TABLE_ID,
        VIEW_TABLE_ID,
        MAPPING_TABLE_ID,
        TVF_TABLE_ID,
        STATUS,
        SCHEMA_TYPE,
        FUNCTION_TYPE,
        IS_DELETE,
        CREATE_USER,
        CREATE_TIME,
        EDIT_USER,
        EDIT_TIME
    )
    VALUES (
        @ID,
        @FORM_NAME,
        @FORM_CODE,
        @FORM_DESCRIPTION,
        NULL,
        NULL,
        @VIEW_TABLE_NAME,
        NULL,
        NULL,
        NULL,
        NULL,
        @VIEW_TABLE_ID,
        NULL,
        NULL,
        @STATUS,
        @SCHEMA_TYPE,
        @FUNCTION_TYPE,
        0,
        @CREATE_USER,
        @CREATE_TIME,
        @EDIT_USER,
        @EDIT_TIME
    )

OUTPUT INSERTED.ID;";
    }
}
