using DcMateH5.Abstractions.Form.Models;

namespace DcMateH5.Abstractions.Form.FormLogic;

public interface IFormFieldConfigService
{
    Task<List<FormFieldConfigDto>> GetFormFieldConfigAsync(Guid? id, CancellationToken ct = default);

    Task<FieldConfigData> LoadFieldConfigDataAsync(Guid? masterId, CancellationToken ct = default);

    // 相容舊呼叫端（逐步淘汰）
    List<FormFieldConfigDto> GetFormFieldConfig(Guid? id);

    FieldConfigData LoadFieldConfigData(Guid? masterId);
}
