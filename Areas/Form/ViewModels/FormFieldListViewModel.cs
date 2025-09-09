using ClassLibrary;
using DcMateH5Api.Areas.Form.Models;

namespace DcMateH5Api.Areas.Form.ViewModels;

public class FormFieldListViewModel
{
    public List<FormFieldViewModel> Fields { get; set; } = new();
}

