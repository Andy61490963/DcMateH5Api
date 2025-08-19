using ClassLibrary;

namespace DynamicForm.Areas.Form.ViewModels;

public class FormFieldViewModel
{
    /// <summary>
    /// PK
    /// </summary>
    public Guid ID { get; set; }

    /// <summary>
    /// 這個欄位是否為Pk
    /// </summary>
    public bool IS_PK { get; set; }

    /// <summary>
    /// parent
    /// </summary>
    public Guid FORM_FIELD_Master_ID { get; set; }

    /// <summary>
    /// 表名稱
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// 欄位名稱
    /// </summary>
    public string COLUMN_NAME { get; set; } = string.Empty;

    /// <summary>
    /// 欄位資料結構類型
    /// </summary>
    public string DATA_TYPE { get; set; } = string.Empty;

    /// <summary>
    /// 預設值
    /// </summary>
    public string DEFAULT_VALUE { get; set; } = string.Empty;

    /// <summary>
    /// 是否可以編輯
    /// </summary>
    public bool IS_EDITABLE { get; set; } = true;

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool IS_REQUIRED { get; set; } = true;
    
    /// <summary>
    /// 排序
    /// </summary>
    public int FIELD_ORDER { get; set; }

    /// <summary>
    /// 是否有輸入限制條件
    /// </summary>
    public bool IS_VALIDATION_RULE { get; set; }

    /// <summary>
    /// 控制類別
    /// </summary>
    public FormControlType? CONTROL_TYPE { get; set; }

    /// <summary>
    /// 查詢元件類型，決定此欄位在搜尋條件中的顯示方式。
    /// </summary>
    public QueryConditionType QUERY_CONDITION_TYPE { get; set; } = QueryConditionType.None;

    /// <summary>
    /// 若查詢元件為下拉選單，使用此 SQL 取得選項資料。
    /// </summary>
    // public string QUERY_CONDITION_SQL { get; set; } = string.Empty;

    /// <summary>
    /// 是否允許作為查詢條件欄位。
    /// </summary>
    public bool CAN_QUERY { get; set; }

    /// <summary>
    /// 控制選擇白名單
    /// </summary>
    public List<FormControlType> CONTROL_TYPE_WHITELIST { get; set; } = new();

    /// <summary>
    /// 查詢條件元件選擇白名單
    /// </summary>
    public List<QueryConditionType> QUERY_CONDITION_TYPE_WHITELIST { get; set; } = new();

    /// <summary>
    /// 資料表查詢類型，更新欄位設定後重新載入清單時使用
    /// </summary>
    public TableSchemaQueryType SchemaType { get; set; }
}

