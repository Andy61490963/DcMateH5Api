using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Form.Models;

[Table("FORM_FIELD_DELETE_GUARD_SQL")]
public class FormFieldDeleteGuardSqlDto
{
    [Key]
    [Column("ID")]
    public Guid ID { get; set; }

    [Column("FORM_FIELD_MASTER_ID")]
    public Guid? FORM_FIELD_MASTER_ID { get; set; }

    [Column("NAME")]
    public string? NAME { get; set; }

    [Column("GUARD_SQL")]
    public string? GUARD_SQL { get; set; }

    [Column("IS_ENABLED")]
    public bool? IS_ENABLED { get; set; }

    [Column("RULE_ORDER")]
    public int? RULE_ORDER { get; set; }

    [Column("CREATE_USER")]
    public Guid? CREATE_USER { get; set; }

    [Column("CREATE_TIME")]
    public DateTime? CREATE_TIME { get; set; }

    [Column("EDIT_USER")]
    public Guid? EDIT_USER { get; set; }

    [Column("EDIT_TIME")]
    public DateTime? EDIT_TIME { get; set; }

    [Column("IS_DELETE")]
    public bool? IS_DELETE { get; set; }
}
