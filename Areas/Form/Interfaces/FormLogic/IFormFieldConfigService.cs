using DynamicForm.Areas.Form.Models;
using ClassLibrary;

namespace DynamicForm.Areas.Form.Interfaces.FormLogic;

public interface IFormFieldConfigService
{
    List<FormFieldConfigDto> GetFormFieldConfig(Guid? id);

    FieldConfigData LoadFieldConfigData(Guid masterId);
}