namespace DynamicForm.Areas.Form.ViewModels;

public class DropdownAnswerDto
{
    public string RowId { get; set; } = default!;
    public Guid FieldId { get; set; }
    public Guid OptionId { get; set; }
}

