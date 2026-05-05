using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Wip.Model;

[Table("ADM_OPI_USER")]
public class AdmUserDto
{
    [Key]
    public Guid USER_SID { get; set; }
    public string ACCOUNT_NO { get; set; } = null!;
    public decimal? SHIFT_SID { get; set; }

    [NotMapped]
    public decimal? WORKGROUP_SID { get; set; }
}

[Table("EQM_MASTER")]
public class EqmMasterDto
{
    [Key]
    public decimal EQM_MASTER_SID { get; set; }
    public string EQM_MASTER_NO { get; set; } = null!;
    public string? EQM_MASTER_NAME { get; set; }
}

[Table("WIP_WO")]
public class WipWoDto
{
    [Key]
    public decimal WO_SID { get; set; }
    public string WO { get; set; } = null!;
    public string? PART_NO { get; set; }
    public string? ROUTE_NO { get; set; }
    public decimal? FACTORY_SID { get; set; }
    public decimal? RELEASE_QTY { get; set; }
}

[Table("WIP_OPERATION")]
public class WipOperationDto
{
    [Key]
    public decimal WIP_OPERATION_SID { get; set; }
    public string WIP_OPERATION_NO { get; set; } = null!;
    public string? WIP_OPERATION_NAME { get; set; }
    public int? SEQ { get; set; }
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
    public Guid? ADM_USER_SID { get; set; }
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

[Table("WIP_OPI_WDOEACICO_HIST_DC")]
public class WipOpiWdoeacicoHistDcDto
{
    [Key]
    public decimal WIP_OPI_WDOEACICO_HIST_DC_SID { get; set; }
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public string? DATA_TYPE { get; set; }
    public string? DC_TYPE { get; set; }
    public decimal DC_ITEM_SID { get; set; }
    public string DC_ITEM_CODE { get; set; } = null!;
    public string DC_ITEM_NAME { get; set; } = null!;
    public decimal DC_ITEM_SEQ { get; set; }
    public string? DC_ITEM_VALUE { get; set; }
    public string? DC_ITEM_COMMENT { get; set; }
    public string? USL { get; set; }
    public string? UCL { get; set; }
    public string? TARGET { get; set; }
    public string? LCL { get; set; }
    public string? LSL { get; set; }
    public string? THROW_SPC { get; set; }
    public string? THROW_SPC_RESULT { get; set; }
    public decimal? SPC_RESULT_LINK_SID { get; set; }
    public string? RESULT { get; set; }
    public string? RESULT_COMMENT { get; set; }
    public string? QC_NO { get; set; }
}
