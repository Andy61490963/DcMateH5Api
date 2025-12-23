using System.ComponentModel.DataAnnotations;

namespace ClassLibrary.SystemError;

/// <summary>
/// 非預期、未被處理的系統例外
/// </summary>
public enum SystemErrorCode
{
    [Display(Name = "系統發生未預期錯誤")]
    UnhandledException,

    [Display(Name = "資料庫錯誤")]
    DatabaseError,

    [Display(Name = "外部服務呼叫失敗")]
    ExternalServiceError,

    [Display(Name = "系統逾時")]
    Timeout
}