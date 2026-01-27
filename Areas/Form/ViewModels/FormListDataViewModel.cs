namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 資料列表用 ViewModel
/// </summary>
public class FormListDataViewModel
{
    public Guid FormMasterId { get; set; }
    public string FormName { get; set; } = string.Empty;  
    public Guid? BaseId { get; set; }
    public string Pk { get; set; } = string.Empty;        // 主鍵值（字串化，方便前端直接使用）
    public List<FormFieldInputViewModel> Fields { get; set; } = new();
}
