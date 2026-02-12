using DcMateH5.Abstractions.Form.Models;

namespace DcMateH5.Abstractions.Form.ViewModels;
public class DropDownViewModel
{
    public FormFieldDropDownDto FormFieldDropDown { get; set; }
    public List<FormFieldDropdownOptionsDto> OPTION_TEXT { get; set; }
}