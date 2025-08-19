using System;
using System.Collections.Generic;

namespace DynamicForm.Areas.Form.ViewModels;

/// <summary>
/// ViewModel for submitting form data from client.
/// </summary>
public class FormSubmissionInputModel
{
    public Guid FormId { get; set; }
    public string? Pk { get; set; }
    public string? TargetTableToUpsert { get; set; }
    public List<FormInputField> InputFields { get; set; } = new();
}
