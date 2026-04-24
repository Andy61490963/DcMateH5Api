namespace DcMateH5.Abstractions.Form.ViewModels;

public class ViewFormConfigViewModel
{
    public Guid Id { get; set; }

    public string FormName { get; set; } = string.Empty;

    public Guid ViewTableId { get; set; }

    public string ViewTableName { get; set; } = string.Empty;
}
