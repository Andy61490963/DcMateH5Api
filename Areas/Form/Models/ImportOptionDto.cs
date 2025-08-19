using System.ComponentModel.DataAnnotations;

namespace DynamicForm.Areas.Form.Models;

public class ImportOptionDto
{
    /// <summary>
    /// 使用者輸入的 SQL 語法（例如 SELECT Value, Text FROM ...）
    /// </summary>
    [Required(ErrorMessage = "SQL 語法不可為空")]
    public string Sql { get; set; }

    // /// <summary>
    // /// 選項來源的資料表名稱（用於記錄來源或驗證）
    // /// </summary>
    // [Required(ErrorMessage = "來源資料表不可為空")]
    // public string OptionTable { get; set; }
}

