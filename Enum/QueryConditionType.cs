using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

/// <summary>
/// 查詢條件元件類型，用於決定搜尋介面所使用的輸入元件。
/// </summary>
public enum QueryConditionType
{
    /// <summary>
    /// 無
    /// </summary>
    [Display(Name = "無", Description = "沒有定義")]
    None = 0,
    
    /// <summary>
    /// 以單行文字輸入作為條件。
    /// </summary>
    [Display(Name = "文字", Description = "以單行文字輸入作為條件")]
    Text = 1,

    /// <summary>
    /// 數值輸入條件。
    /// </summary>
    [Display(Name = "數字", Description = "以數值作為輸入條件")]
    Number = 2,

    /// <summary>
    /// 日期輸入條件。
    /// </summary>
    [Display(Name = "日期", Description = "以日期作為輸入條件")]
    Date = 3,

    /// <summary>
    /// 下拉選單條件。
    /// </summary>
    [Display(Name = "下拉選單", Description = "以下拉選單作為條件")]
    Dropdown = 4
}
