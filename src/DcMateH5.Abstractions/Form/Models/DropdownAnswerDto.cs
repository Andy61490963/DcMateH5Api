namespace DcMateH5.Abstractions.Form.Models;

public class DropdownAnswerDto
{
    public string RowId { get; set; } = default!;
    public Guid FieldId { get; set; }
    public Guid OptionId { get; set; }
}

