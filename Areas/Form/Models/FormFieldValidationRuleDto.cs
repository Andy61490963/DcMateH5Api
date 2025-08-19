using ClassLibrary;

namespace DynamicForm.Areas.Form.Models;

public class FormFieldValidationRuleDto
{
    public Guid ID { get; set; }
    public Guid FIELD_CONFIG_ID { get; set; }
    public ValidationType? VALIDATION_TYPE { get; set; } = ValidationType.Regex;
    public string VALIDATION_VALUE { get; set; }
    public string MESSAGE_ZH { get; set; }
    public string MESSAGE_EN { get; set; }
    public int VALIDATION_ORDER { get; set; }
}