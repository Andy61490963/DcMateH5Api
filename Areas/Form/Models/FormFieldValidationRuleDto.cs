using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClassLibrary;

namespace DcMateH5Api.Areas.Form.Models;

[Table("FORM_FIELD_VALIDATION_RULE")]
public class FormFieldValidationRuleDto
{
    [Key]
    [Column("ID")]
    public Guid ID { get; set; }
    
    [Column("FORM_FIELD_CONFIG_ID")]
    public Guid FORM_FIELD_CONFIG_ID { get; set; }
    
    [Column("VALIDATION_TYPE")]
    public ValidationType? VALIDATION_TYPE { get; set; } = ValidationType.Regex;
    
    [Column("VALIDATION_VALUE")]
    public string VALIDATION_VALUE { get; set; }
    
    [Column("MESSAGE_ZH")]
    public string MESSAGE_ZH { get; set; }
    
    [Column("MESSAGE_EN")]
    public string MESSAGE_EN { get; set; }
    
    [Column("VALIDATION_ORDER")]
    public int VALIDATION_ORDER { get; set; }
}