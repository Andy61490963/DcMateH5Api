using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum ValidationType
{
    [Display(Name = "最大值", Description = "限制條件-最大值")]
    Max = 1,

    [Display(Name = "最小值", Description = "限制條件-最小值")]
    Min = 2,

/// <summary>
/// 正則表達式（Regular Expression）。
/// 適用於欄位需要特定格式驗證的情境，例如 Email、手機號碼、數字等。
/// </summary>
[Display(
    Name = "正則表達式",
    Description =
@"常用正則表達式範例：
1. Email：^[^@\s]+@[^@\s]+\.[^@\s]+$
2. 手機號碼（台灣）：^09\d{8}$
3. 數字（整數）：^\d+$
4. 小數：^\d+(\.\d+)?$
5. 英文字母：^[A-Za-z]+$
6. 英數混合：^[A-Za-z0-9]+$
7. 身分證字號（台灣）：^[A-Z][12]\d{8}$
8. 日期（yyyy-MM-dd）：^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$
9. URL：^(https?|ftp)://[^\s/$.?#].[^\s]*$
"
)]
Regex = 3


    // [Display(Name = "Email 格式")]
    // Email = 4,
    //
    // [Display(Name = "數值格式")]
    // Number = 5
}
