using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

/// <summary>
/// 表單欄位的控制元件類型，用於決定 UI 呈現方式與資料格式
/// </summary>
public enum DropDownType
{
    [Display(Name = "Sql匯入", Description = "Sql匯入")]
    Sql,
    
    [Display(Name = "手動打", Description = "手動打")]
    NotSql,
    
    [Display(Name = "全部", Description = "全部")]
    All
}