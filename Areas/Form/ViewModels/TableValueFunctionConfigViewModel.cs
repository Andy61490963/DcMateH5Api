namespace DcMateH5Api.Areas.Form.ViewModels;

public class TableValueFunctionConfigViewModel
{
    /// <summary>
    /// FORM_FIELD_MASTER 的唯一識別碼。
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// TVF ID 名稱。
    /// </summary>
    public Guid TableFunctionValueId { get; set; }
    
    /// <summary>
    /// 設定檔顯示名稱。
    /// </summary>
    public string FormName { get; set; } = string.Empty;

    /// <summary>
    /// TVF 名稱。
    /// </summary>
    public string TableFunctionValueName { get; set; } = string.Empty;
    
    /// <summary>
    /// TVF 參數
    /// </summary>
    public List<string>? Parameter { get; set; }
}