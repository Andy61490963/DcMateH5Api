namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 儲存 Master/Detail 表單主檔資訊使用的 ViewModel。
/// </summary>
public class MasterDetailFormHeaderViewModel
{
    /// <summary>
    /// FORM_FIELD_Master 主檔 ID
    /// </summary>
    public Guid ID { get; set; }

    /// <summary>
    /// 表單名稱
    /// </summary>
    public string FORM_NAME { get; set; }

    /// <summary>
    /// Master 表單的 FORM_FIELD_Master ID
    /// </summary>
    public Guid BASE_TABLE_ID { get; set; }

    /// <summary>
    /// Detail 表單的 FORM_FIELD_Master ID
    /// </summary>
    public Guid DETAIL_TABLE_ID { get; set; }

    /// <summary>
    /// Master+Detail View 的 FORM_FIELD_Master ID
    /// </summary>
    public Guid VIEW_TABLE_ID { get; set; }
}
