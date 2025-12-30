namespace DcMateH5Api.Areas.Form.ViewModels;

public class FormFieldDeleteGuardSqlUpdateViewModel
{
    /// <summary>
    /// 對應的表單主檔 ID（可為空，表示尚未綁定）
    /// </summary>
    public Guid? FORM_FIELD_MASTER_ID { get; set; }

    /// <summary>
    /// 規則名稱
    /// </summary>
    public string? NAME { get; set; }

    /// <summary>
    /// 刪除防呆 SQL（回傳筆數 > 0 即視為不可刪）
    /// </summary>
    public string? GUARD_SQL { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool? IS_ENABLED { get; set; }

    /// <summary>
    /// 規則排序
    /// </summary>
    public int? RULE_ORDER { get; set; }
}
