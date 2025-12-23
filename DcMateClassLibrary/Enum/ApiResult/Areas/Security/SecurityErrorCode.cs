using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum AuthenticationErrorCode
{
    [Display(Name = "查無帳號", Description = "查無帳號")]
    UserNotFound,
    
    [Display(Name = "帳號或密碼錯誤", Description = "帳號或密碼錯誤")]
    PasswordInvalid,
    
    [Display(Name = "帳號已存在", Description = "帳號已存在")]
    AccountAlreadyExists,
    
    [Display(Name = "註冊失敗，請稍後再試", Description = "註冊失敗，請稍後再試")]
    RegisterFailed,
    
    [Display(Name = "未登入或 Token 無效", Description = "未登入或 Token 無效")]
    Unauthorized
}