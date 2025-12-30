namespace DcMateH5Api.Areas.Form.ViewModels;

public class DeleteGuardValidateRequestViewModel
{
    /// <summary>
    /// 表單欄位主檔 ID，用於查詢對應的刪除守門規則。
    /// </summary>
    public Guid FormFieldMasterId { get; set; }

    /// <summary>
    /// Guard SQL 參數名稱（不含 @），用於對應 SQL 內的參數。
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Guard SQL 參數值，將以參數化方式帶入 SQL。
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
