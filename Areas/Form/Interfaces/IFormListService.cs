using DynamicForm.Areas.Form.Models;
using ClassLibrary;

namespace DynamicForm.Areas.Form.Interfaces;

public interface IFormListService
{
    List<FORM_FIELD_Master> GetFormMasters();
    FORM_FIELD_Master? GetFormMaster(Guid id);
    void DeleteFormMaster(Guid id);
}