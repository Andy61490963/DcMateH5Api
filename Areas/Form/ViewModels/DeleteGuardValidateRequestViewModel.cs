namespace DcMateH5Api.Areas.Form.ViewModels;

public class DeleteWithGuardRequestViewModel
{
    /// <summary>
    /// 表單欄位主檔 ID（傳 baseTable 的主檔 ID）
    /// </summary>
    public Guid FormFieldMasterId { get; set; }

    /// <summary>
    /// 要刪除的資料的主鍵
    /// </summary>
    public string pk { get; set; }
    
    /// <summary>
    /// Guard SQL 參數集合（Key = 參數名稱，不含 @）
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}


/// <summary>
/// 合併 Guard 驗證 + 物理刪除的結果
/// </summary>
public class DeleteWithGuardResultViewModel
{
    /// <summary>
    /// Guard 設定/SQL/參數是否有效
    /// - false：代表 Guard SQL 本身不合法、缺參數、或 Guard SQL 沒回傳預期欄位
    /// - true：代表 Guard 流程本身沒問題（接下來看 CanDelete）
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 是否允許刪除（Guard 驗證通過才會是 true）
    /// </summary>
    public bool CanDelete { get; init; }

    /// <summary>
    /// 被哪一條規則擋下（CanDelete = false 時才會有值）
    /// </summary>
    public string? BlockedByRule { get; init; }

    /// <summary>
    /// Guard 流程錯誤訊息（IsValid = false 時才會有值）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 是否實際刪除成功（只在 CanDelete = true 且執行刪除後才有意義）
    /// - true：刪到資料（affected > 0）
    /// - false：沒刪到（可能 row 不存在）
    /// </summary>
    public bool Deleted { get; init; }
}
