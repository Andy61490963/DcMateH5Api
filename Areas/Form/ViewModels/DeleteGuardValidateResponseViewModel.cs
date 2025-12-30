namespace DcMateH5Api.Areas.Form.ViewModels;

public class DeleteGuardValidateDataViewModel
{
    /// <summary>
    /// 是否允許刪除。
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// 阻擋刪除的規則名稱。
    /// </summary>
    public string? BlockedByRule { get; set; }
}

public class DeleteGuardValidateResultViewModel
{
    /// <summary>
    /// 是否為有效的 Guard SQL 驗證流程。
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 驗證失敗原因（僅在 IsValid=false 時使用）。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 是否允許刪除。
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// 阻擋刪除的規則名稱。
    /// </summary>
    public string? BlockedByRule { get; set; }
}
