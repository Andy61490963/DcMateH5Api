using System.ComponentModel.DataAnnotations;

namespace DcMateH5.Abstractions.Form.ViewModels;
public class ReplaceDropdownOptionsViewModel
{
    /// <summary>
    /// 下拉選項清單：送完整清單，後端會用此清單覆蓋 DB 現況
    /// </summary>
    [Required]
    [MinLength(0)]
    public List<DropdownOptionItemViewModel> Options { get; set; } = new();
}

public class DropdownOptionItemViewModel
{
    [Required(ErrorMessage = "選項文字不可為空")]
    public string OptionText { get; set; } = "";

    [Required(ErrorMessage = "選項值不可為空")]
    public string OptionValue { get; set; } = "";

    [StringLength(255, ErrorMessage = "選項類型長度不可超過 255 個字元")]
    public string? OptionType { get; set; }
}

