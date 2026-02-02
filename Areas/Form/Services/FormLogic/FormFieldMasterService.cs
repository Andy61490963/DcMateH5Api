using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.SqlHelper;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DcMateH5Api.Services.CurrentUser.Interfaces;

namespace DcMateH5Api.Areas.Form.Services.FormLogic;

public class FormFieldMasterService : IFormFieldMasterService
{
    private readonly SqlConnection _con;
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly ICurrentUserAccessor _currentUser;

    public FormFieldMasterService(SqlConnection connection, SQLGenerateHelper sqlHelper, ICurrentUserAccessor currentUser)
    {
        _con = connection;
        _sqlHelper = sqlHelper;
        _currentUser = currentUser;
    }

    private Guid GetCurrentUserId()
    {
        var user = _currentUser.Get();
        return user.Id;
    }

    public FormFieldMasterDto? GetFormFieldMaster(TableSchemaQueryType type)
    {
        return _con.QueryFirstOrDefault<FormFieldMasterDto>(
            "/**/SELECT * FROM FORM_FIELD_MASTER WHERE SCHEMA_TYPE = @TYPE",
            new { TYPE = type.ToInt() });
    }

    public FormFieldMasterDto GetFormFieldMasterFromId(Guid? id, SqlTransaction? tx = null)
    {
        return _con.QueryFirst<FormFieldMasterDto>(
            "/**/SELECT * FROM FORM_FIELD_MASTER WHERE ID = @id",
            new { id }, transaction: tx);
    }

    /// <summary>
    /// 依主鍵取得 FORM_FIELD_MASTER（非交易版）。
    /// </summary>
    public Task<FormFieldMasterDto?> GetFormFieldMasterFromIdAsync(Guid? id, CancellationToken ct = default)
    {
        if (id == null) return Task.FromResult<FormFieldMasterDto?>(null);

        var where = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, id.Value);

        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    /// <summary>
    /// 依主鍵取得 FORM_FIELD_MASTER（交易內版）。
    /// </summary>
    public Task<FormFieldMasterDto?> GetFormFieldMasterFromIdInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        Guid? id,
        CancellationToken ct = default)
    {
        if (id == null) return Task.FromResult<FormFieldMasterDto?>(null);

        var where = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, id.Value);

        return _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, where, ct: ct);
    }

    /// <summary>
    /// 取得或建立 FORM_FIELD_MASTER（非交易版）。
    /// </summary>
    public Task<Guid> GetOrCreateAsync(FormFieldMasterDto model, CancellationToken ct = default)
    {
        return _sqlHelper.TxAsync((conn, tx, ct) => GetOrCreateInTxAsync(conn, tx, model, ct), ct: ct);
    }

    /// <summary>
    /// 取得或建立 FORM_FIELD_MASTER（交易內版）。
    /// - 以 UPDLOCK/HOLDLOCK 避免併發競態，確保同一筆只插入一次。
    /// </summary>
    public async Task<Guid> GetOrCreateInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        FormFieldMasterDto model,
        CancellationToken ct = default)
    {
        var id = model.ID == Guid.Empty ? Guid.NewGuid() : model.ID;

        // 使用 UPDLOCK/HOLDLOCK 在交易內鎖定同一筆 Key，避免併發重複插入
        var existing = await conn.QueryFirstOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "/**/SELECT TOP (1) ID FROM FORM_FIELD_MASTER WITH (UPDLOCK, HOLDLOCK) WHERE ID = @id",
                new { id },
                tx,
                cancellationToken: ct));

        if (existing.HasValue)
        {
            return existing.Value;
        }

        static bool HasValue(string? s) => !string.IsNullOrWhiteSpace(s);

        // 同一交易內寫入主檔，確保建立與後續操作一致
        var command = new CommandDefinition(
            @"
    INSERT INTO FORM_FIELD_MASTER
    (
        ID, FORM_NAME, STATUS, SCHEMA_TYPE,
        BASE_TABLE_NAME, VIEW_TABLE_NAME, DETAIL_TABLE_NAME, MAPPING_TABLE_NAME, TVF_TABLE_NAME,
        BASE_TABLE_ID,   VIEW_TABLE_ID,   DETAIL_TABLE_ID,   MAPPING_TABLE_ID,   TVF_TABLE_ID,
        FUNCTION_TYPE, IS_DELETE, CREATE_TIME, EDIT_TIME, CREATE_USER, EDIT_USER
    )
    VALUES
    (
        @ID, @FORM_NAME, @STATUS, @SCHEMA_TYPE,
        @BASE_TABLE_NAME, @VIEW_TABLE_NAME, @DETAIL_TABLE_NAME, @MAPPING_TABLE_NAME, @TVF_TABLE_NAME,
        @BASE_TABLE_ID,   @VIEW_TABLE_ID,   @DETAIL_TABLE_ID,   @MAPPING_TABLE_ID,   @TVF_TABLE_ID,
        @FUNCTION_TYPE, 0, GETDATE(), GETDATE(), @CREATE_USER, @EDIT_USER
    );",
            new
            {
                ID = id,
                model.FORM_NAME,
                model.STATUS,
                model.SCHEMA_TYPE,
                model.BASE_TABLE_NAME,
                model.VIEW_TABLE_NAME,
                model.DETAIL_TABLE_NAME,
                model.MAPPING_TABLE_NAME,
                model.TVF_TABLE_NAME, // 確保這裡 DTO 也有值

                // 下面這些 ID 的判斷邏輯維持不變
                BASE_TABLE_ID = HasValue(model.BASE_TABLE_NAME) ? id : (Guid?)null,
                VIEW_TABLE_ID = HasValue(model.VIEW_TABLE_NAME) ? id : (Guid?)null,
                DETAIL_TABLE_ID = HasValue(model.DETAIL_TABLE_NAME) ? id : (Guid?)null,
                MAPPING_TABLE_ID = HasValue(model.MAPPING_TABLE_NAME) ? id : (Guid?)null,
                TVF_TABLE_ID = HasValue(model.TVF_TABLE_NAME) ? id : (Guid?)null,

                model.FUNCTION_TYPE,
                CREATE_USER = GetCurrentUserId(),
                EDIT_USER = GetCurrentUserId()
            },
            tx,
            cancellationToken: ct);

        await conn.ExecuteAsync(command);
        return id;
    }

    public List<(FormFieldMasterDto Master, List<FormFieldConfigDto> FieldConfigs)> GetFormMetaAggregates(FormFunctionType funcType, TableSchemaQueryType type)
    {
        var masters = _con.Query<FormFieldMasterDto>(
            "/**/SELECT * FROM FORM_FIELD_MASTER WHERE SCHEMA_TYPE = @TYPE AND FUNCTION_TYPE = @funcType",
            new { TYPE = type.ToInt(), funcType = funcType.ToInt() })
            .ToList();

        var result = new List<(FormFieldMasterDto Master, List<FormFieldConfigDto> FieldConfigs)>();

        foreach (var master in masters)
        {
            var configs = _con.Query<FormFieldConfigDto>(
                "/**/SELECT ID, COLUMN_NAME, CONTROL_TYPE, CAN_QUERY FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_MASTER_ID = @id",
                new { id = master.BASE_TABLE_ID })
                .ToList();

            result.Add((master, configs));
        }

        return result;
    }

    public async Task<List<(FormFieldMasterDto Master, List<FormFieldConfigDto> FieldConfigs)>> GetFormMetaAggregatesAsync(
        FormFunctionType funcType, TableSchemaQueryType type, CancellationToken ct = default)
    {
        var masters = (await _con.QueryAsync<FormFieldMasterDto>(
            new CommandDefinition(
                "/**/SELECT * FROM FORM_FIELD_MASTER WHERE SCHEMA_TYPE = @TYPE AND FUNCTION_TYPE = @funcType",
                new { TYPE = type.ToInt(), funcType = funcType.ToInt() },
                cancellationToken: ct)))
            .ToList();

        var result = new List<(FormFieldMasterDto Master, List<FormFieldConfigDto> FieldConfigs)>();

        foreach (var master in masters)
        {
            var configs = (await _con.QueryAsync<FormFieldConfigDto>(
                new CommandDefinition(
                    "/**/SELECT ID, COLUMN_NAME, CONTROL_TYPE, CAN_QUERY FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_MASTER_ID = @id",
                    new { id = master.BASE_TABLE_ID },
                    cancellationToken: ct)))
                .ToList();

            result.Add((master, configs));
        }

        return result;
    }
}
