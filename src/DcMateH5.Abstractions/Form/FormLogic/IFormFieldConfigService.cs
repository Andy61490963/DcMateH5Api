using DcMateH5.Abstractions.Form.Models;

namespace DcMateH5.Abstractions.Form.FormLogic;


public interface IFormFieldConfigService
{
    List<FormFieldConfigDto> GetFormFieldConfig(Guid? id);

    FieldConfigData LoadFieldConfigData(Guid? masterId);
}