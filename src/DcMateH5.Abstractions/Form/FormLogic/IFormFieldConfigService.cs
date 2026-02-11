using DcMateH5.Abstractions.Form.Models;

namespace DcMateH5.Abstractions.Form.FormLogic;

public interface IFormFieldConfigService
{
    Task<List<FormFieldConfigDto>> GetFormFieldConfigAsync(Guid? id, CancellationToken ct = default);

    Task<FieldConfigData> LoadFieldConfigDataAsync(Guid? masterId, CancellationToken ct = default);

    // 同步方法（僅供既有模組遷移期間使用）
    List<FormFieldConfigDto> GetFormFieldConfig(Guid? id);

    FieldConfigData LoadFieldConfigData(Guid? masterId);
}
