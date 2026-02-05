using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Wip.Model;

[Table("UMM_USER")]
public class UmmUserDto
{
    [Key]
    public decimal USER_SID { get; set; }
    public string ACCOUNT_NO { get; set; } = null!;
}

[Table("EQM_MASTER")]
public class EqmMasterDto
{
    [Key]
    public decimal EQM_MASTER_SID { get; set; }
    public string EQM_MASTER_NO { get; set; } = null!;
}

[Table("WIP_WO")]
public class WipWoDto
{
    [Key]
    public decimal WO_SID { get; set; }
    public string WO { get; set; } = null!;
}

[Table("WIP_OPERATION")]
public class WipOperationDto
{
    [Key]
    public decimal WIP_OPERATION_SID { get; set; }
    public string WIP_OPERATION_NO { get; set; } = null!;
}

[Table("WIP_DEPARTMENT")]
public class WipDepartmentDto
{
    [Key]
    public decimal DEPT_SID { get; set; }
    public string DEPT_NO { get; set; } = null!;
}

[Table("WIP_OPI_WDOEACICO_HIST")]
public class WipOpiWdoeacicoHistDto
{
    [Key]
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public string? WO { get; set; }
    public string? DEPT_NO { get; set; }
    public string? OPERATION_CODE { get; set; }
    public string? EQP_NO { get; set; }
    public DateTime? CHECK_IN_TIME { get; set; }
    public DateTime? CHECK_OUT_TIME { get; set; }
    public string? COMPLETED { get; set; }
    public string? COMMENT { get; set; }
    public decimal? TOTAL_OK_QTY { get; set; }
    public decimal? TOTAL_NG_QTY { get; set; }
}

[Table("WIP_OPI_WDOEACICO_HIST_USER")]
public class WipOpiWdoeacicoHistUserDto
{
    [Key]
    public decimal WIP_OPI_WDOEACICO_HIST_USER_SID { get; set; }
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public decimal? UMM_USER_SID { get; set; }
    public string? ACCOUNT_NO { get; set; }
}

[Table("WIP_OPI_WDOEACICO_HIST_EQP")]
public class WipOpiWdoeacicoHistEqpDto
{
    [Key]
    public decimal WIP_OPI_WDOEACICO_HIST_EQP_SID { get; set; }
    public decimal? WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public string? EQP_NO { get; set; }
    public string? ENABLE_FLAG { get; set; }
}

[Table("WIP_OPI_WDOEACICO_HIST_DETAIL")]
public class WipOpiWdoeacicoHistDetailDto
{
    [Key]
    public decimal WIP_OPI_WDOEACICO_HIST_DETAIL_SID { get; set; }
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public decimal OK_QTY { get; set; }
    public decimal NG_QTY { get; set; }
    public decimal NG_REASON_QTY { get; set; }
    public string COMMENT { get; set; }
    public string ENABLE_FLAG { get; set; }
}

[Table("WIP_OPI_WDOEACICO_HIST_NG_REASON_DETAIL")]
public class WipOpiWdoeacicoHistNgReasonDetailDto
{
    [Key]
    public decimal WIP_OPI_WDOEACICO_HIST_NG_REASON_DETAIL_SID { get; set; }
    public decimal WIP_OPI_WDOEACICO_HIST_DETAIL_SID { get; set; }
    public decimal NG_QTY { get; set; }
    public string NG_CODE { get; set; }
    public string COMMENT { get; set; }
    public string ENABLE_FLAG { get; set; }
}
