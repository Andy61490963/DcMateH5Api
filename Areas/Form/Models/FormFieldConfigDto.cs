using ClassLibrary;

namespace DynamicForm.Areas.Form.Models;

public class FormFieldConfigDto
{
    public Guid ID { get; set; }

    public Guid FORM_FIELD_Master_ID { get; set; }

    public string FORM_NAME { get; set; } = string.Empty;

    public string TABLE_NAME { get; set; } = string.Empty;
    
    public string SOURCE_TABLE { get; set; }

    public string COLUMN_NAME { get; set; } = string.Empty;

    public FormControlType CONTROL_TYPE { get; set; }

    /// <summary>
    /// 查詢元件類型，決定該欄位在搜尋介面上的呈現方式。
    /// </summary>
    public QueryConditionType QUERY_CONDITION_TYPE { get; set; }

    /// <summary>
    /// 若為下拉選單查詢條件，使用此 SQL 取得選項資料。
    /// </summary>
    // public string? QUERY_CONDITION_SQL { get; set; }

    /// <summary>
    /// 是否允許此欄位作為查詢條件使用。
    /// </summary>
    public bool CAN_QUERY { get; set; }
    
    public string? DEFAULT_VALUE { get; set; }

    public bool IS_REQUIRED { get; set; }

    public bool IS_EDITABLE { get; set; }

    public int FIELD_ORDER { get; set; }

    public string DATA_TYPE { get; set; }
}