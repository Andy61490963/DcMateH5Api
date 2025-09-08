using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 回傳主表與明細表填寫資料的 ViewModel。
/// </summary>
public class FormMasterDetailSubmissionViewModel
{
    /// <summary>主表資料。</summary>
    public FormSubmissionViewModel Master { get; set; } = null!;

    /// <summary>明細表資料清單。</summary>
    public List<FormSubmissionViewModel> Details { get; set; } = new();
}
