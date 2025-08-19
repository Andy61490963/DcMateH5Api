namespace DynamicForm.Areas.Form.ViewModels;

public class FormHeaderViewModel
{
    /// <summary>
    /// FORM_FIELD_Master 主檔 ID
    /// </summary>
    public Guid ID { get; set; }

    /// <summary>
    /// 主檔名稱
    /// </summary>
    public string FORM_NAME { get; set; }

    /// <summary>
    /// 表名稱
    /// </summary>
    public string TABLE_NAME { get; set; }

    /// <summary>
    /// 前台顯示表名稱
    /// </summary>
    public string VIEW_TABLE_NAME { get; set; }

    /// <summary>
    /// 主要表單 Master ID
    /// </summary>
    public Guid BASE_TABLE_ID { get; set; }

    /// <summary>
    /// View 表單 Master ID
    /// </summary>
    public Guid VIEW_TABLE_ID { get; set; }
}

