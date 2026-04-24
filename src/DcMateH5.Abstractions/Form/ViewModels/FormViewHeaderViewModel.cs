namespace DcMateH5.Abstractions.Form.ViewModels;

public class FormViewHeaderViewModel
{
    public Guid ID { get; set; }

    public string FORM_NAME { get; set; } = string.Empty;

    public string FORM_CODE { get; set; } = string.Empty;

    public string FORM_DESCRIPTION { get; set; } = string.Empty;

    public Guid VIEW_TABLE_ID { get; set; }
}
