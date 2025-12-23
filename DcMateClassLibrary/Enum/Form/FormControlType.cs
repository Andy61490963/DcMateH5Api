using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

/// <summary>
/// 表單欄位的控制元件類型，用於決定 UI 呈現方式與資料格式
/// </summary>
public enum FormControlType
{
    /// <summary>
    /// None
    /// </summary>
    [Display(Name = "無", Description = "沒有定義")]
    None,
    
    /// <summary>
    /// 單行文字輸入欄位（對應 input type="text"）。
    /// 適用於一般名稱、代碼、簡短描述等文字資料。
    /// </summary>
    [Display(Name = "文字", Description = "input type=text")]
    Text,

    /// <summary>
    /// 數值欄位（對應 input type="number"）。
    /// 適用於數量、金額、百分比等數值資料。
    /// </summary>
    [Display(Name = "數字", Description = "input type=number")]
    Number,

    /// <summary>
    /// 日期選擇欄位（對應 input type="date"）。
    /// 適用於日期輸入，例如生日、起訖日等。
    /// </summary>
    [Display(Name = "日期", Description = "input type=date")]
    Date,

    /// <summary>
    /// 勾選框（對應 input type="checkbox"）。
    /// 適用於布林值，例如是否啟用、是否同意等。
    /// </summary>
    [Display(Name = "確認按鈕", Description = "input type=checkbox")]
    Checkbox,

    /// <summary>
    /// 多行文字輸入框（對應 textarea）。
    /// 適用於備註、描述、意見欄等較長文字資料。
    /// </summary>
    [Display(Name = "文字輸入框", Description = "input type=textarea")]
    Textarea,

    /// <summary>
    /// 下拉選單（對應 select 元件）。
    /// 可設定靜態選項或透過 SQL 匯入選項。
    /// </summary>
    [Display(Name = "下拉選單", Description = "input type=select")]
    Dropdown
}