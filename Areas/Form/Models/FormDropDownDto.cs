using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Form.Models;

[Table("FORM_FIELD_DROPDOWN")]
public class FormDropDownDto
{
    [Key]
    [Column("ID")]
    public Guid ID { get; set; }  
    
    [Column("FORM_FIELD_CONFIG_ID")]
    public Guid FORM_FIELD_CONFIG_ID { get; set; }  
    
    [Column("ISUSESQL")]
    public bool ISUSESQL { get; set; }  
    
    [Column("DROPDOWNSQL")]
    public string DROPDOWNSQL { get; set; } = string.Empty;

    [Column("IS_QUERY_DROPDOWN")]
    public bool IS_QUERY_DROPDOWN { get; set; }
}
