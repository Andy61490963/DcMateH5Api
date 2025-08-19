using System.Collections.Generic;

namespace DynamicForm.Areas.Form.Models;

public class FORM_FIELD_DROPDOWN
{
    public Guid ID { get; set; }  
    public Guid FORM_FIELD_CONFIG_ID { get; set; }  
    public bool ISUSESQL { get; set; }  
    public string DROPDOWNSQL { get; set; } = string.Empty;
}
