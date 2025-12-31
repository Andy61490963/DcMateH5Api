using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClassLibrary;

namespace DcMateH5Api.Areas.Form.Models;

[Table("FORM_FIELD_CONFIG")]
public class FormFieldConfigDto
{
    [Key]
    [Column("ID")]
    public Guid ID { get; set; }

    [Column("FORM_FIELD_MASTER_ID")]
    public Guid FORM_FIELD_MASTER_ID { get; set; }

    [Column("TABLE_NAME")]
    public string TABLE_NAME { get; set; } = string.Empty;

    [Column("COLUMN_NAME")]
    public string COLUMN_NAME { get; set; } = string.Empty;
    
    [Column("DATA_TYPE")]
    public string DATA_TYPE { get; set; }

    [Column("CONTROL_TYPE")]
    public FormControlType CONTROL_TYPE { get; set; }

    /// <summary>
    /// 是否允許此欄位作為查詢條件使用。
    /// </summary>
    [Column("CAN_QUERY")]
    public bool CAN_QUERY { get; set; }
    
    /// <summary>
    /// 查詢元件類型，決定該欄位在搜尋介面上的呈現方式。
    /// </summary>
    [Column("QUERY_COMPONENT")]
    public QueryComponentType QUERY_COMPONENT { get; set; }

    /// <summary>
    /// 實際查詢 SQL 條件
    /// </summary>
    [Column("QUERY_CONDITION")]
    public ConditionType QUERY_CONDITION { get; set; }
    
    [Column("QUERY_DEFAULT_VALUE")]
    public string? QUERY_DEFAULT_VALUE { get; set; }

    [Column("IS_EDITABLE")]
    public bool IS_EDITABLE { get; set; }
    
    [Column("IS_REQUIRED")]
    public bool IS_REQUIRED { get; set; }

    [Column("FIELD_ORDER")]
    public long FIELD_ORDER { get; set; }
}