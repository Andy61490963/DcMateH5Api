using System.ComponentModel.DataAnnotations;

namespace DcMateH5Api.Areas.Form.Models;

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
}

