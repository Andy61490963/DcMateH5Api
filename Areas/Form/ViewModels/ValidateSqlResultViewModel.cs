namespace DynamicForm.Areas.Form.ViewModels;

public class ValidateSqlResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int RowCount { get; set; }

    // 回傳結果資料（可顯示前 N 筆）
    public List<Dictionary<string, object>> Rows { get; set; } = new();
}
