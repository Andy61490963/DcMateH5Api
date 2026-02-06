namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 表單列表回應（每張表單一包）
/// </summary>
public sealed class FormListResponseViewModel
{
    public Guid FormMasterId { get; set; }
    public string FormName { get; set; } = string.Empty;
    public Guid? BaseId { get; set; }

    /// <summary>
    /// 總筆數或總頁數（依你系統定義）。
    /// 目前你的實作是 GetTotalCount()，建議未來改名為 TotalCount。
    /// </summary>
    public int TotalPageSize { get; set; }

    public List<FormListRowViewModel> Items { get; set; } = new();
}

/// <summary>
/// 表單列表每一列（真正會變動的資料）
/// </summary>
public sealed class FormListRowViewModel
{
    public string Pk { get; set; } = string.Empty;
    public List<FormFieldInputViewModel> Fields { get; set; } = new();
}