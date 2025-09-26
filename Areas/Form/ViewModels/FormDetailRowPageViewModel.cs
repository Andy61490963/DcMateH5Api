using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 封裝明細列的分頁結果。
/// </summary>
public class FormDetailRowPageViewModel
{
    /// <summary>目前頁碼（從 1 起算）。</summary>
    public int Page { get; set; }

    /// <summary>每頁筆數。</summary>
    public int PageSize { get; set; }

    /// <summary>總筆數，供前端計算頁數。</summary>
    public int TotalCount { get; set; }

    /// <summary>主檔與明細的關聯欄位名稱。</summary>
    public string RelationColumn { get; set; } = string.Empty;

    /// <summary>當前頁面的明細列。</summary>
    public List<FormDetailRowViewModel> Rows { get; set; } = new();
}
