namespace DcMateH5Api.Areas.Form.ViewModels;

public class FormTvfListDataViewModel
{
    public Guid FormMasterId { get; set; }
    public string FormName { get; set; } = string.Empty;  
    public List<FormFieldInputViewModel> Fields { get; set; } = new();
}