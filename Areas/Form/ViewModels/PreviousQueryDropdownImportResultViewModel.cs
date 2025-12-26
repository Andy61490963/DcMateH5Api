namespace DcMateH5Api.Areas.Form.ViewModels;

public class PreviousQueryDropdownImportResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public List<string> Values { get; set; } = new();
}
