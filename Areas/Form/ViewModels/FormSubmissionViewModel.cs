using DynamicForm.Areas.Form.Models;

namespace DynamicForm.Areas.Form.ViewModels;

public class FormSubmissionViewModel
{
    public Guid FormId { get; set; }
    public string? Pk { get; set; }
    public string? TargetTableToUpsert { get; set; }
    public string FormName { get; set; }
    public List<FormFieldInputViewModel> Fields { get; set; }
}

