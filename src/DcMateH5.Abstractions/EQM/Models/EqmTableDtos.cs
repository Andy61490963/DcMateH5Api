using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5.Abstractions.Eqm.Models;

[Table("EQM_MASTER")]
public class EqmMasterDto
{
    [Key]
    public decimal EQM_MASTER_SID { get; set; }
    public string EQM_MASTER_NO { get; set; } = null!;
    public string EQM_MASTER_NAME { get; set; } = null!;
    public decimal? STATUS_SID { get; set; }
    public string? STATUS { get; set; }
    public DateTime? EDIT_STATUS_TIME { get; set; }
    public DateTime? STATUS_CHANGE_TIME { get; set; }
    public string ENABLE_FLAG { get; set; } = null!;
    public string? EDIT_USER { get; set; }
    public DateTime? EDIT_TIME { get; set; }
}

[Table("EQM_STATUS")]
public class EqmStatusDto
{
    [Key]
    public decimal EQM_STATUS_SID { get; set; }
    public string EQM_STATUS_NO { get; set; } = null!;
    public string EQM_STATUS_NAME { get; set; } = null!;
    public string ENABLE_FLAG { get; set; } = null!;
    public string? OEE_TYPE { get; set; }
}

[Table("ADM_REASON")]
public class EqmReasonDto
{
    [Key]
    public decimal ADM_REASON_SID { get; set; }
    public string? REASON_NO { get; set; }
    public string? REASON_NAME { get; set; }
    public string? ENABLE_FLAG { get; set; }
}

public class EqmStatusChangeHistRow
{
    public decimal TO_EQM_STATUS_SID { get; set; }
    public string TO_EQM_STATUS_CODE { get; set; } = null!;
    public string TO_EQM_STATUS_NAME { get; set; } = null!;
    public string TO_EQM_STATUS_OEE_TYPE { get; set; } = null!;
    public DateTime REPORT_TIME { get; set; }
}

public class EqmShiftInfoRow
{
    public string SHIFT_NO { get; set; } = string.Empty;
    public string REPORT_DAY { get; set; } = string.Empty;
}
