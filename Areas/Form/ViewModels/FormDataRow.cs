using DynamicForm.Areas.Form.Models;

namespace DynamicForm.Areas.Form.ViewModels;

public class FormDataRow
{
    public object PkId { get; set; }
    public List<FormDataCell> Cells { get; set; } = new();

    public object? GetValue(string columnName)
    {
        foreach (var cell in Cells)
        {
            if (string.Equals(cell.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                return cell.Value;
        }
        return null;
    }

    public object? this[string columnName] => GetValue(columnName);
}