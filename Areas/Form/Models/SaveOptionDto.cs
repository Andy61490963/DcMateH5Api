using System.ComponentModel.DataAnnotations;

namespace DynamicForm.Areas.Form.Models;

public class SaveOptionDto
{
    /// <summary>
    /// 選項 ID，若為 null 表示新增；否則表示更新
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// 顯示用的選項文字（例如「是 / 否」、「已完成 / 未完成」）
    /// </summary>
    [Required(ErrorMessage = "選項文字不可為空")]
    public string OptionText { get; set; }

    /// <summary>
    /// 實際存入資料庫的值（例如 true/false、1/0）
    /// </summary>
    [Required(ErrorMessage = "選項值不可為空")]
    public string OptionValue { get; set; }
}

