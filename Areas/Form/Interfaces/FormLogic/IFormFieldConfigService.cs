using DcMateH5Api.Areas.Form.Models;
using ClassLibrary;

namespace DcMateH5Api.Areas.Form.Interfaces.FormLogic;

public interface IFormFieldConfigService
{
    List<FormFieldConfigDto> GetFormFieldConfig(Guid? id);

    FieldConfigData LoadFieldConfigData(Guid? masterId);
}