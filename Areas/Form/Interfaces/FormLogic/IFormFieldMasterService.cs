using DcMateH5Api.Areas.Form.Models;
using ClassLibrary;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DcMateH5Api.Areas.Form.Interfaces.FormLogic;

public interface IFormFieldMasterService
{
    FormFieldMasterDto? GetFormFieldMaster(TableSchemaQueryType type);

    FormFieldMasterDto GetFormFieldMasterFromId(Guid? id, SqlTransaction? tx = null);

    /// <summary>
    /// 非交易版本：依主鍵取得 FORM_FIELD_MASTER。
    /// </summary>
    Task<FormFieldMasterDto?> GetFormFieldMasterFromIdAsync(Guid? id, CancellationToken ct = default);

    /// <summary>
    /// 交易版本：依主鍵取得 FORM_FIELD_MASTER。
    /// </summary>
    Task<FormFieldMasterDto?> GetFormFieldMasterFromIdInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        Guid? id,
        CancellationToken ct = default);

    /// <summary>
    /// 非交易版本：若不存在則建立並回傳主鍵。
    /// </summary>
    Task<Guid> GetOrCreateAsync(FormFieldMasterDto model, CancellationToken ct = default);

    /// <summary>
    /// 交易版本：若不存在則建立並回傳主鍵。
    /// </summary>
    Task<Guid> GetOrCreateInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        FormFieldMasterDto model,
        CancellationToken ct = default);

    List<(FormFieldMasterDto Master, List<FormFieldConfigDto> FieldConfigs)> GetFormMetaAggregates(
        FormFunctionType funcType, TableSchemaQueryType type);

    Task<List<(FormFieldMasterDto Master, List<FormFieldConfigDto> FieldConfigs)>> GetFormMetaAggregatesAsync(
        FormFunctionType funcType, TableSchemaQueryType type, CancellationToken ct = default);
}
