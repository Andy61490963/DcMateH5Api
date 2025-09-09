using System.ComponentModel.DataAnnotations;

namespace DcMateH5Api.Areas.Form.Models;

public class ImportOptionDto
{
    /// <summary>
    /// 使用者輸入的 SQL 語法（例如 SELECT Value, Text FROM ...）
    /// </summary>
    [Required(ErrorMessage = "SQL 語法不可為空")]
    public string Sql { get; set; }
}

