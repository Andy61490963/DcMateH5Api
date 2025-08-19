using DynamicForm.Areas.Form.Models;

namespace DynamicForm.Areas.Form.ViewModels;

public class DropDownViewModel
{
    public Guid ID { get; set; }  
    public Guid FORM_FIELD_CONFIG_ID { get; set; }  
    public bool ISUSESQL { get; set; }  
    public string DROPDOWNSQL { get; set; } = string.Empty;
    public List<FORM_FIELD_DROPDOWN_OPTIONS> OPTION_TEXT { get; set; } = new();
}