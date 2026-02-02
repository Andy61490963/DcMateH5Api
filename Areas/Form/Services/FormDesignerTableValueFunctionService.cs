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
    private const string DefaultSchemaName = "dbo";

    private readonly SqlConnection _con;
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IFormDesignerService _formDesignerService;
    private readonly ISchemaService _schemaService;
    private readonly IReadOnlyList<string> _relationColumnSuffixes;

    public FormDesignerTableValueFunctionService(
        SQLGenerateHelper sqlHelper,
        SqlConnection connection,
        IOptions<FormSettings> formSettings,
        IFormDesignerService formDesignerService,
        ISchemaService schemaService,
        ICurrentUserAccessor currentUser)
    {
        _con = connection;
        _sqlHelper = sqlHelper;
        _formDesignerService = formDesignerService;
        _schemaService = schemaService;
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
    /// 確保指定 TVF 的欄位設定已同步保存，並回傳欄位清單（交易內）。
    /// </summary>
    /// <param name="tvfName">TVF 名稱</param>
    /// <param name="formMasterId">既有主檔 ID（可為 null）</param>
    /// <param name="schemaType">Schema 類型</param>
    /// <param name="ct">CancellationToken</param>
    public async Task<FormFieldListViewModel?> EnsureFieldsSaved(string tvfName, Guid? formMasterId, TableSchemaQueryType schemaType, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return await _sqlHelper.TxAsync(async (conn, tx, ct) =>
        {
            var columns = await _schemaService.GetObjectSchemaInTxAsync(conn, tx, DefaultSchemaName, tvfName, ct).ConfigureAwait(false);
            if (columns.Count == 0)
            {
                return null;
            }

            var masterId = await _formDesignerService
                .ResolveMasterIdAsync(conn, tx, tvfName, formMasterId, schemaType, ct)
                .ConfigureAwait(false);

            var configs = await _formDesignerService
                .GetFieldConfigsInTxAsync(conn, tx, tvfName, masterId, ct)
                .ConfigureAwait(false);

            await _formDesignerService
                .UpsertMissingConfigsInTxAsync(conn, tx, tvfName, masterId, schemaType, columns, configs, ct)
                .ConfigureAwait(false);

            return await GetFieldsByTableNameInTxAsync(conn, tx, tvfName, masterId, schemaType, ct).ConfigureAwait(false);
        }, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 儲存 TVF 表單主檔（Upsert），回傳主檔 ID（交易內）。
    /// </summary>
    public async Task<Guid> SaveTableValueFunctionFormHeader(FormHeaderTableValueFunctionViewModel model, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return await _sqlHelper.TxAsync(async (conn, tx, ct) =>
        {
            var tvfMaster = await GetTvfMasterAsync(conn, tx, model.TVF_TABLE_ID, ct).ConfigureAwait(false);
            var tvfTableName = tvfMaster.TVF_TABLE_NAME;

            var currentUserId = GetCurrentUserId();
            var now = DateTime.Now;

            var id = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
                Sql.UpsertFormMasterTvf,
                new
                {
                    model.ID,
                    model.FORM_NAME,
                    model.FORM_CODE,
                    model.FORM_DESCRIPTION,

                    TVF_TABLE_ID = model.TVF_TABLE_ID,
                    TVF_TABLE_NAME = tvfTableName,

                    STATUS = (int)TableStatusType.Active,
                    SCHEMA_TYPE = TableSchemaQueryType.All,
                    FUNCTION_TYPE = FormFunctionType.TableValueFunctionMaintenance,

                    EDIT_TIME = now,
                    EDIT_USER = currentUserId,
                    CREATE_TIME = now,
                    CREATE_USER = currentUserId
                },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);

            return id;
        }, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 對外公開：以「內部自包 Tx」方式取得欄位清單（給 Controller 用）。
    /// </summary>
    public async Task<FormFieldListViewModel> GetFieldsByTableNameInTxAsync(
        string tvfName,
        Guid? formMasterId,
        TableSchemaQueryType schemaType,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return await _sqlHelper.TxAsync(async (conn, tx, ct) =>
        {
            return await GetFieldsByTableNameInTxAsync(conn, tx, tvfName, formMasterId, schemaType, ct).ConfigureAwait(false);
        }, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 交易內：取得欄位清單（核心邏輯）。
    /// </summary>
    public async Task<FormFieldListViewModel> GetFieldsByTableNameInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        string tvfName,
        Guid? formMasterId,
        TableSchemaQueryType schemaType,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var columns = await _schemaService.GetObjectSchemaInTxAsync(conn, tx, DefaultSchemaName, tvfName, ct).ConfigureAwait(false);
        if (columns.Count == 0)
        {
            return new FormFieldListViewModel { Fields = new List<FormFieldViewModel>() };
        }

        var configs = await _formDesignerService
            .GetFieldConfigsInTxAsync(conn, tx, tvfName, formMasterId, ct)
            .ConfigureAwait(false);

        var masterId = ResolveMasterId(formMasterId, configs);

        var fields = BuildFieldViewModels(columns, configs, masterId, tvfName, schemaType);

        return new FormFieldListViewModel
        {
            Fields = SortFields(fields, columns)
        };
    }

    private static Guid ResolveMasterId(Guid? formMasterId, IReadOnlyDictionary<string, FormFieldConfigDto> configs)
    {
        if (formMasterId.HasValue)
        {
            return formMasterId.Value;
        }

        // 防呆：configs 可能是空（例如尚未建立任何 config）
        if (configs.Count == 0)
        {
            return Guid.Empty; // 保持行為：原本會炸，現在改成安全回傳；若你不接受 Guid.Empty，可改成 throw。
        }

        return configs.Values.First().FORM_FIELD_MASTER_ID;
    }

    private List<FormFieldViewModel> BuildFieldViewModels(
        List<DbColumnInfo> columns,
        IReadOnlyDictionary<string, FormFieldConfigDto> configs,
        Guid masterId,
        string tvfName,
        TableSchemaQueryType schemaType)
    {
        var fields = new List<FormFieldViewModel>(columns.Count);

        foreach (var col in columns)
        {
            var columnName = col.COLUMN_NAME;
            var hasCfg = configs.TryGetValue(columnName, out var cfg);

            var vm = CreateFieldViewModel(
                cfg,
                columnName,
                col.DATA_TYPE,
                col.isTvfQueryParameter,
                col.SourceIsNullable,
                masterId,
                tvfName,
                schemaType);

            _formDesignerService.ApplySchemaPolicy(vm, schemaType);

            fields.Add(vm);
        }

        return fields;
    }

    private static FormFieldViewModel CreateFieldViewModel(
        FormFieldConfigDto? cfg,
        string columnName,
        string dataType,
        bool isTvfQueryParameter,
        bool isNullable,
        Guid masterId,
        string tvfName,
        TableSchemaQueryType schemaType)
    {
        var fieldId = cfg != null ? cfg.ID : Guid.NewGuid();

        return new FormFieldViewModel
        {
            ID = fieldId,
            FORM_FIELD_MASTER_ID = masterId,
            FORM_FIELD_DROPDOWN_ID = null,
            TableName = tvfName,

            IsNullable = isNullable,
            COLUMN_NAME = columnName,
            DATA_TYPE = dataType,
            IS_TVF_QUERY_PARAMETER = isTvfQueryParameter,

            DISPLAY_NAME = cfg?.DISPLAY_NAME ?? columnName,
            CONTROL_TYPE = cfg?.CONTROL_TYPE,

            IS_REQUIRED = cfg?.IS_REQUIRED ?? false,
            IS_EDITABLE = cfg?.IS_EDITABLE ?? true,
            IS_DISPLAYED = cfg?.IS_DISPLAYED ?? true,

            IS_PK = false,

            QUERY_DEFAULT_VALUE = cfg?.QUERY_DEFAULT_VALUE,
            QUERY_COMPONENT = cfg?.QUERY_COMPONENT ?? QueryComponentType.None,
            QUERY_CONDITION = cfg?.QUERY_CONDITION,
            CAN_QUERY = cfg?.CAN_QUERY ?? false,

            FIELD_ORDER = cfg?.FIELD_ORDER,
            DETAIL_TO_RELATION_DEFAULT_COLUMN = cfg?.DETAIL_TO_RELATION_DEFAULT_COLUMN,

            // 你原本就塞 null 的保留
            CONTROL_TYPE_WHITELIST = null,
            QUERY_COMPONENT_TYPE_WHITELIST = null,
            IS_VALIDATION_RULE = null,

            SchemaType = schemaType
        };
    }

    private static List<FormFieldViewModel> SortFields(List<FormFieldViewModel> fields, List<DbColumnInfo> columns)
    {
        // 防止 FIELD_ORDER 大量 null 導致排序不穩定：fallback 用 schema ordinal（columns 原本就有 ORDINAL_POSITION）
        var ordinalMap = columns
            .GroupBy(x => x.COLUMN_NAME, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Min(x => x.ORDINAL_POSITION), StringComparer.OrdinalIgnoreCase);

        return fields
            .OrderBy(f => f.FIELD_ORDER ?? int.MaxValue)
            .ThenBy(f =>
            {
                if (ordinalMap.TryGetValue(f.COLUMN_NAME, out var ord))
                {
                    return ord;
                }

                return int.MaxValue;
            })
            .ThenBy(f => f.COLUMN_NAME, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<FormFieldMasterDto> GetTvfMasterAsync(SqlConnection conn, SqlTransaction tx, Guid tvfTableId, CancellationToken ct)
    {
        var whereTvf = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, tvfTableId)
            .AndNotDeleted();

        // 你原本是 _sqlHelper.SelectFirstOrDefaultAsync(whereTvf)（可能沒吃 tx）
        // 這裡假設 SQLGenerateHelper 有 Tx 版；如果沒有，就改成用 conn/tx 自己 query（不新增套件）。
        var tvfMaster = await _sqlHelper.SelectFirstOrDefaultInTxAsync<FormFieldMasterDto>(conn, tx, whereTvf).ConfigureAwait(false);

        if (tvfMaster == null)
        {
            throw new InvalidOperationException("TVF 查無資料");
        }

        return tvfMaster;
    }

    private static class Sql
    {
        public const string UpsertFormMasterTvf = @"
MERGE FORM_FIELD_MASTER AS target
USING (SELECT @ID AS ID) AS src
ON target.ID = src.ID

WHEN MATCHED THEN
    UPDATE SET
        FORM_NAME          = @FORM_NAME,
        FORM_CODE          = @FORM_CODE,
        FORM_DESCRIPTION   = @FORM_DESCRIPTION,

        -- 只更新 TVF 相關欄位，避免影響其他類型
        TVF_TABLE_NAME     = @TVF_TABLE_NAME,
        TVF_TABLE_ID       = @TVF_TABLE_ID,

        STATUS             = @STATUS,
        SCHEMA_TYPE        = @SCHEMA_TYPE,
        FUNCTION_TYPE      = @FUNCTION_TYPE,
        EDIT_USER          = @EDIT_USER,
        EDIT_TIME          = @EDIT_TIME

WHEN NOT MATCHED THEN
    INSERT (
        ID,
        FORM_NAME,
        FORM_CODE,
        FORM_DESCRIPTION,
        TVF_TABLE_NAME,
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
        @TVF_TABLE_NAME,
        @TVF_TABLE_ID,
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
