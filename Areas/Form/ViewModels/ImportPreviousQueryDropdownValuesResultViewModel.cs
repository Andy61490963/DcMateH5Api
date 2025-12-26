namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 匯入歷史查詢下拉結果回應。
/// </summary>
public class ImportPreviousQueryDropdownValuesResultViewModel
{
    /// <summary>
    /// 是否匯入成功。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 匯入訊息（成功或錯誤原因）。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 匯入筆數。
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// 匯入的 NAME 清單（全量或可依需求裁切）。
    /// </summary>
    public List<string> Values { get; set; } = new();
}
