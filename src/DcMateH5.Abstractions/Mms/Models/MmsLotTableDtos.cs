using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5.Abstractions.Mms.Models;

[Table("MMS_MLOT")]
public class MmsMLotDto
{
    [Key]
    public decimal MLOT_SID { get; set; }
    public string MLOT { get; set; } = null!;
    public string? MLOT_TYPE { get; set; }
    public string? MLOT_WO { get; set; }
    public string PART_NO { get; set; } = null!;
    public decimal MLOT_QTY { get; set; }
    public string? PARENT_MLOT { get; set; }
    public string MLOT_STATUS_CODE { get; set; } = null!;
    public DateTime? EXPIRY_DATE { get; set; }
    public string? ALIAS_MLOT1 { get; set; }
    public string? ALIAS_MLOT2 { get; set; }
    public string? DATE_CODE { get; set; }
    public decimal? TOTAL_MLOT_CONSUME_QTY { get; set; }
    public decimal? TOTAL_MLOT_NG_QTY { get; set; }
    public DateTime? LAST_TRANS_TIME { get; set; }
    public DateTime? LAST_STATUS_CHANGE_TIME { get; set; }
    public decimal? LAST_MMS_MLOT_HIST_SID { get; set; }
    public string? MMS_LOCATION_01_NO { get; set; }
    public string? MMS_LOCATION_02_NO { get; set; }
    public string? MMS_LOCATION_03_NO { get; set; }
    public string? MMS_LOCATION_04_NO { get; set; }
    public string? COMMENT { get; set; }
    public string CREATE_USER { get; set; } = null!;
    public DateTime CREATE_TIME { get; set; }
    public string EDIT_USER { get; set; } = null!;
    public DateTime EDIT_TIME { get; set; }
}

[Table("MMS_MLOT_HIST")]
public class MmsMLotHistDto
{
    [Key]
    public decimal MMS_MLOT_HIST_SID { get; set; }
    public decimal? DATA_LINK_SID { get; set; }
    public decimal MLOT_SID { get; set; }
    public string MLOT { get; set; } = null!;
    public string? ALIAS_MLOT1 { get; set; }
    public string? ALIAS_MLOT2 { get; set; }
    public string MLOT_STATUS_CODE { get; set; } = null!;
    public string? PRE_MLOT_STATUS_CODE { get; set; }
    public decimal? LOT_SID { get; set; }
    public string? LOT { get; set; }
    public string? WO { get; set; }
    public string PART_NO { get; set; } = null!;
    public decimal? MLOT_QTY { get; set; }
    public decimal? TRANSATION_QTY { get; set; }
    public decimal? BOH_MLOT_QTY { get; set; }
    public string? RELATION_MLOT { get; set; }
    public string? ACTION_CODE { get; set; }
    public decimal? REASON_SID { get; set; }
    public string? REASON_CODE { get; set; }
    public string? INPUT_FORM_NAME { get; set; }
    public string? CREATE_USER { get; set; }
    public DateTime? CREATE_TIME { get; set; }
    public DateTime? REPORT_TIME { get; set; }
    public decimal? PRE_MMS_MLOT_HIST_SID { get; set; }
    public string? MMS_LOCATION_01_NO { get; set; }
    public string? MMS_LOCATION_02_NO { get; set; }
    public string? MMS_LOCATION_03_NO { get; set; }
    public string? MMS_LOCATION_04_NO { get; set; }
    public string? COMMENT { get; set; }
}

[Table("MMS_MLOT_STATUS")]
public class MmsMLotStatusDto
{
    [Key]
    public decimal MLOT_STATUS_SID { get; set; }
    public string MLOT_STATUS_CODE { get; set; } = null!;
    public string? MLOT_STATUS_NAME { get; set; }
    public string? MLOT_STATUS_DESC { get; set; }
    public string CREATE_USER { get; set; } = null!;
    public DateTime CREATE_TIME { get; set; }
    public string EDIT_USER { get; set; } = null!;
    public DateTime EDIT_TIME { get; set; }
    public string? CUR_FLAG { get; set; }
}

[Table("WIP_LOT_KP_CUR_USED")]
public class WipLotKpCurUsedDto
{
    [Key]
    public decimal WIP_LOT_KP_CUR_USED_SID { get; set; }
    public decimal DATA_LINK_SID { get; set; }
    public decimal WIP_LOT_SID { get; set; }
    public string WIP_LOT { get; set; } = null!;
    public decimal? MLOT_SID { get; set; }
    public string? MLOT_TYPE { get; set; }
    public string? MLOT { get; set; }
    public string? PART_NO { get; set; }
    public string? PARENT_MLOT { get; set; }
    public decimal? MLOT_BOH_QTY { get; set; }
    public decimal? MLOT_TRANSATION_QTY { get; set; }
    public decimal? MLOT_QTY { get; set; }
    public string? MLOT_COMMENT { get; set; }
    public DateTime? CREATE_TIME { get; set; }
    public string? COMMENT { get; set; }
}
