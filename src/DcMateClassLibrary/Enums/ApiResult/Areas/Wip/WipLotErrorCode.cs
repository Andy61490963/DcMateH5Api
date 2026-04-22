using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum WipLotErrorCode
{
    [Display(Name = "資料驗證失敗", Description = "資料驗證失敗")]
    BadRequest,

    [Display(Name = "資料衝突", Description = "資料衝突")]
    Conflict,

    [Display(Name = "系統錯誤", Description = "系統錯誤")]
    UnhandledException
}
