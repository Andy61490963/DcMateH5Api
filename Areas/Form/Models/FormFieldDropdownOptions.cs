using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Form.Models;

[Table("FORM_FIELD_DROPDOWN_OPTIONS")]
public class FormFieldDropdownOptions
{
    [Key]
    [Column("ID")]
    public Guid ID { get; set; }
    
    [Column("FORM_FIELD_DROPDOWN_ID")]
    public Guid FORM_FIELD_DROPDOWN_ID { get; set; }
    
    [Column("OPTION_TABLE")]
    public string? OPTION_TABLE { get; set; }
    
    [Column("OPTION_VALUE")]
    public string OPTION_VALUE { get; set; } = string.Empty;
    
    [Column("OPTION_TEXT")]
    public string OPTION_TEXT { get; set; }
}
