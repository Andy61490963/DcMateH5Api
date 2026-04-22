using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Wip.Model;

[Table("WIP_LOT")]
public class WipLotDto
{
    [Key]
    public decimal LOT_SID { get; set; }
    public string LOT { get; set; } = null!;
    public string? ALIAS_LOT1 { get; set; }
    public string? ALIAS_LOT2 { get; set; }
    public string LOT_TYPE { get; set; } = null!;
    public string PARENT_LOT_SID { get; set; } = null!;
    public string PARENT_LOT { get; set; } = null!;
    public string? CREATE_USER { get; set; }
    public DateTime? CREATE_TIME { get; set; }
    public string? EDIT_USER { get; set; }
    public DateTime? EDIT_TIME { get; set; }
    public decimal LOT_STATUS_SID { get; set; }
    public string LOT_STATUS_CODE { get; set; } = null!;
    public decimal WO_SID { get; set; }
    public string WO { get; set; } = null!;
    public string CUR_RULE_CODE { get; set; } = string.Empty;
    public decimal OPERATION_SID { get; set; }
    public decimal OPERATION_SEQ { get; set; }
    public decimal PART_SID { get; set; }
    public string PART_NO { get; set; } = null!;
    public decimal LOT_QTY { get; set; }
    public decimal NG_QTY { get; set; }
    public decimal CUR_OPER_OUT_QTY { get; set; }
    public decimal CUR_OPER_NG_OUT_QTY { get; set; }
    public string CUR_OPERATION_LINK_SID { get; set; } = null!;
    public decimal FACTORY_SID { get; set; }
    public string COMMENT { get; set; } = string.Empty;
    public DateTime? LAST_TRANS_TIME { get; set; }
    public DateTime LAST_STATUS_CHANGE_TIME { get; set; }
    public decimal? LOT_QTY1 { get; set; }
    public decimal? LOT_QTY2 { get; set; }
    public string? LOCATION { get; set; }
    public decimal ROUTE_SID { get; set; }
    public decimal? ROUTE_OPER_SID { get; set; }
    public decimal CUR_OPER_BATCH_ID { get; set; }
    public decimal ALL_OPER_BATCH_ID { get; set; }
    public DateTime? CUR_OPER_FIRST_IN_TIME { get; set; }
    public string CUR_OPER_FIRST_IN_FLAG { get; set; } = null!;
    public string? LOT_SUB_STATUS_CODE { get; set; }
}

[Table("WIP_LOT_HIST")]
public class WipLotHistDto
{
    [Key]
    public decimal WIP_LOT_HIST_SID { get; set; }
    public int SEQ { get; set; }
    public decimal DATA_LINK_SID { get; set; }
    public decimal LOT_SID { get; set; }
    public string LOT { get; set; } = null!;
    public string? ALIAS_LOT1 { get; set; }
    public string? ALIAS_LOT2 { get; set; }
    public decimal LOT_STATUS_SID { get; set; }
    public string LOT_STATUS_CODE { get; set; } = null!;
    public decimal PRE_LOT_STATUS_SID { get; set; }
    public string PRE_LOT_STATUS_CODE { get; set; } = null!;
    public decimal WO_SID { get; set; }
    public string WO { get; set; } = null!;
    public decimal OPERATION_LINK_SID { get; set; }
    public decimal OPERATION_SID { get; set; }
    public string OPERATION_CODE { get; set; } = null!;
    public string OPERATION_NAME { get; set; } = null!;
    public decimal OPERATION_SEQ { get; set; }
    public string OPERATION_FINISH { get; set; } = null!;
    public decimal PART_SID { get; set; }
    public string PART_NO { get; set; } = null!;
    public decimal LOT_QTY { get; set; }
    public decimal TOTAL_OK_QTY { get; set; }
    public decimal TOTAL_NG_QTY { get; set; }
    public decimal TOTAL_DEFECT_QTY { get; set; }
    public decimal? TOTAL_USER_COUNT { get; set; }
    public decimal ROUTE_SID { get; set; }
    public string FACTORY_SID { get; set; } = null!;
    public string? FACTORY_CODE { get; set; }
    public string? FACTORY_NAME { get; set; }
    public string ACTION_CODE { get; set; } = null!;
    public string CONTROL_MODE { get; set; } = string.Empty;
    public string? INPUT_FORM_NAME { get; set; }
    public string? CREATE_USER { get; set; }
    public DateTime? CREATE_TIME { get; set; }
    public DateTime REPORT_TIME { get; set; }
    public DateTime PRE_REPORT_TIME { get; set; }
    public DateTime PRE_STATUS_CHANGE_TIME { get; set; }
    public decimal? LOT_QTY1 { get; set; }
    public decimal? LOT_QTY2 { get; set; }
    public string? LOCATION { get; set; }
    public DateTime? OPER_FIRST_CHECK_IN_TIME { get; set; }
    public decimal SHIFT_SID { get; set; }
    public decimal WORKGROUP_SID { get; set; }
    public decimal? NEXT_OPERATION_SID { get; set; }
    public string? NEXT_OPERATION_CODE { get; set; }
    public string? NEXT_OPERATION_NAME { get; set; }
    public decimal? NEXT_OPERATION_SEQ { get; set; }
    public string? LOT_SUB_STATUS_CODE { get; set; }
    public string? COMMENT { get; set; }
}

[Table("WIP_LOT_STATUS")]
public class WipLotStatusDto
{
    [Key]
    public decimal LOT_STATUS_SID { get; set; }
    public string LOT_STATUS_CODE { get; set; } = null!;
}

[Table("WIP_ROUTE")]
public class WipRouteDto
{
    [Key]
    public decimal WIP_ROUTE_SID { get; set; }
    public string? WIP_ROUTE_NO { get; set; }
    public string? WIP_ROUTE_NAME { get; set; }
}

[Table("WIP_ROUTE_OPERATION")]
public class WipRouteOperationDto
{
    [Key]
    public decimal WIP_ROUTE_OPERATION_SID { get; set; }
    public decimal WIP_ROUTE_SID { get; set; }
    public decimal WIP_OPERATION_SID { get; set; }
    public int SEQ { get; set; }
    public string? NO { get; set; }
    public string? NAME { get; set; }
}

[Table("WIP_PARTNO")]
public class WipPartNoDto
{
    [Key]
    public decimal WIP_PARTNO_SID { get; set; }
    public string WIP_PARTNO_NO { get; set; } = null!;
    public string? WIP_PARTNO_NAME { get; set; }
}

[Table("WIP_LOT_CUR_USER")]
public class WipLotCurUserDto
{
    [Key]
    public decimal WIP_LOT_CUR_USER_SID { get; set; }
    public decimal CUR_OPERATION_LINK_SID { get; set; }
    public decimal LOT_SID { get; set; }
    public string LOT { get; set; } = null!;
    public string CREATE_USER { get; set; } = null!;
    public DateTime CREATE_TIME { get; set; }
    public decimal DATA_LINK_SID { get; set; }
}

[Table("WIP_LOT_USER_HIST")]
public class WipLotUserHistDto
{
    [Key]
    public decimal WIP_LOT_USER_HIST_SID { get; set; }
    public decimal IN_WIP_LOT_HIST_SID { get; set; }
    public decimal? OUT_WIP_LOT_HIST_SID { get; set; }
    public string CREATE_USER { get; set; } = null!;
    public string? USER_COMMENT { get; set; }
    public DateTime CREATE_IN_TIME { get; set; }
    public DateTime REPORT_IN_TIME { get; set; }
    public DateTime? CREATE_OUT_TIME { get; set; }
    public DateTime? REPORT_OUT_TIME { get; set; }
    public string OUT_FLAG { get; set; } = null!;
    public decimal OUT_OK_QTY { get; set; }
    public decimal OUT_NG_QTY { get; set; }
    public decimal REPORT_OUT_OK_QTY { get; set; }
    public decimal REPORT_OUT_NG_QTY { get; set; }
    public string? OPERATION_FINISH { get; set; }
    public decimal OPERATION_LINK_SID { get; set; }
    public decimal? SHIFT_SID { get; set; }
    public decimal? WORKGROUP_SID { get; set; }
    public string? OUT_USER { get; set; }
    public decimal? OUT_SHIFT_SID { get; set; }
    public decimal? OUT_WORKGROUP_SID { get; set; }
    public string? LOT_SUB_STATUS_CODE { get; set; }
}

[Table("WIP_LOT_CUR_EQP")]
public class WipLotCurEqpDto
{
    [Key]
    public decimal WIP_LOT_CUR_EQP_SID { get; set; }
    public decimal OPERATION_LINK_SID { get; set; }
    public decimal LOT_SID { get; set; }
    public string LOT { get; set; } = null!;
    public decimal EQP_SID { get; set; }
    public string EQP_NO { get; set; } = null!;
    public DateTime CREATE_TIME { get; set; }
    public decimal? DATA_LINK_SID { get; set; }
}

[Table("WIP_LOT_HOLD_HIST")]
public class WipLotHoldHistDto
{
    [Key]
    public decimal WIP_LOT_HOLD_HIST_SID { get; set; }
    public decimal HOLD_WIP_LOT_HIST_SID { get; set; }
    public string LOT { get; set; } = null!;
    public decimal HOLD_REASON_SID { get; set; }
    public string HOLD_REASON_CODE { get; set; } = null!;
    public string HOLD_REASON_NAME { get; set; } = null!;
    public string? HOLD_REASON_COMMENT { get; set; }
    public decimal PRE_LOT_STATUS_SID { get; set; }
    public decimal? RELEASE_WIP_LOT_HIST_SID { get; set; }
    public decimal? RELEASE_REASON_SID { get; set; }
    public string? RELEASE_REASON_CODE { get; set; }
    public string? RELEASE_REASON_NAME { get; set; }
    public string? RELEASE_REASON_COMMENT { get; set; }
    public string RELEASE_FLAG { get; set; } = null!;
}

[Table("QMM_DC_ITEM")]
public class QmmDcItemDto
{
    [Key]
    public decimal QMM_ITEM_SID { get; set; }
    public string QMM_ITEM_NO { get; set; } = null!;
    public string QMM_ITEM_NAME { get; set; } = null!;
    public string? DATA_TYPE { get; set; }
    public string? ENABLE_FLAG { get; set; }
    public int? SAMPLE_SIZE { get; set; }
    public string? USL { get; set; }
    public string? UCL { get; set; }
    public string? TARGET { get; set; }
    public string? LCL { get; set; }
    public string? LSL { get; set; }
}

[Table("WIP_LOT_DC_ITEM_CURRENT")]
public class WipLotDcItemCurrentDto
{
    [Key]
    public decimal WIP_LOT_DC_ITEM_CURRENT_SID { get; set; }
    public string LOT { get; set; } = null!;
    public decimal WIP_LOT_DC_HIST_SID { get; set; }
    public decimal WIP_LOT_HIST_SID { get; set; }
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
    public DateTime? CREATE_TIME { get; set; }
}

[Table("WIP_LOT_DC_ITEM_HIST")]
public class WipLotDcItemHistDto
{
    [Key]
    public decimal WIP_LOT_DC_HIST_SID { get; set; }
    public decimal WIP_LOT_HIST_SID { get; set; }
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
    public DateTime? CREATE_TIME { get; set; }
}

[Table("ADM_REASON")]
public class AdmReasonDto
{
    [Key]
    public decimal ADM_REASON_SID { get; set; }
    public string REASON_NO { get; set; } = null!;
    public string REASON_NAME { get; set; } = null!;
    public string? REASON_TYPE { get; set; }
    public string? ENABLE_FLAG { get; set; }
}

[Table("WIP_LOT_EQP_HIST")]
public class WipLotEqpHistDto
{
    [Key]
    public decimal WIP_LOT_EQP_HIST_SID { get; set; }
    public decimal WIP_LOT_HIST_SID { get; set; }
    public decimal EQP_SID { get; set; }
    public string EQP_NO { get; set; } = null!;
    public string? EQP_NAME { get; set; }
    public string? EQP_COMMENT { get; set; }
    public DateTime CREATE_TIME { get; set; }
}
