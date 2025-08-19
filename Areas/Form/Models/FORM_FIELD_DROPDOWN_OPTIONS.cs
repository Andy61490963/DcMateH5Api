using System.Collections.Generic;

namespace DynamicForm.Areas.Form.Models;

public class FORM_FIELD_DROPDOWN_OPTIONS
{
    public Guid ID { get; set; }
    public Guid FORM_FIELD_DROPDOWN_ID { get; set; }
    public string? OPTION_TABLE { get; set; } = string.Empty;
    public string OPTION_VALUE { get; set; } = string.Empty;
    public string OPTION_TEXT { get; set; }
}
