using DcMateH5Api.Areas.Form.Models;

namespace DcMateH5Api.Areas.Form.ViewModels;

public class DropDownViewModel
{
    public FormDropDownDto FormDropDown { get; set; }
    public Task<List<FormFieldDropdownOptionsDto>> OPTION_TEXT { get; set; }
}