namespace DcMateH5Api.Areas.Form.ViewModels;

public class FormHeaderViewModel
{
    /// <summary>
    /// FORM_FIELD_MASTER 主檔 ID
    /// </summary>
    public Guid ID { get; set; }

    /// <summary>
    /// 主檔名稱
    /// </summary>
    public string FORM_NAME { get; set; }

    /// <summary>
    /// 主檔代碼
    /// </summary>
    public string FORM_CODE { get; set; }
    
    /// <summary>
    /// 主檔設定描述
    /// </summary>
    public string FORM_DESCRIPTION { get; set; }
    
    /// <summary>
    /// 主要表單 Master ID
    /// </summary>
    public Guid BASE_TABLE_ID { get; set; }

    /// <summary>
    /// View 表單 Master ID
    /// </summary>
    public Guid VIEW_TABLE_ID { get; set; }
}

