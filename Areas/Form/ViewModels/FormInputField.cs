namespace DynamicForm.Areas.Form.ViewModels;

/// <summary>
/// Represent single field input for submission.
/// </summary>
public class FormInputField
{
    public Guid FieldConfigId { get; set; }
    public string? Value { get; set; }
}

