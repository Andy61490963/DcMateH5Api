namespace DcMateH5Api.Areas.Form.ViewModels;

public class DeleteGuardValidateRequestViewModel
{
    /// <summary>
    /// 表單欄位主檔 ID
    /// </summary>
    public Guid FormFieldMasterId { get; set; }

    /// <summary>
    /// Guard SQL 參數集合（Key = 參數名稱，不含 @）
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}
