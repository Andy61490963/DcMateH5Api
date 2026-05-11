namespace DcMateH5.Abstractions.Mms.Models;

public class MmsCreateMLotInputDto
{
    public decimal DATA_LINK_SID { get; set; }
    public string MLOT { get; set; } = null!;
    public string? PARENT_MLOT { get; set; }
    public string? ALIAS_MLOT1 { get; set; }
    public string? ALIAS_MLOT2 { get; set; }
    public string? MLOT_TYPE { get; set; }
    public string PART_NO { get; set; } = null!;
    public decimal MLOT_QTY { get; set; }
    public string? MLOT_WO { get; set; }
    public DateTime? EXPIRY_DATE { get; set; }
    public string? DATE_CODE { get; set; }
    public DateTime? REPORT_TIME { get; set; }
    public string ACCOUNT_NO { get; set; } = null!;
    public string? INPUT_FORM_NAME { get; set; }
    public string? COMMENT { get; set; }
}

public class MmsMLotConsumeInputDto
{
    public decimal DATA_LINK_SID { get; set; }
    public string MLOT { get; set; } = null!;
    public DateTime? REPORT_TIME { get; set; }
    public string ACCOUNT_NO { get; set; } = null!;
    public decimal CONSUME_QTY { get; set; }
    public string LOT { get; set; } = null!;
    public string? INPUT_FORM_NAME { get; set; }
    public string? COMMENT { get; set; }
}

public class MmsMLotUNConsumeInputDto
{
    public decimal DATA_LINK_SID { get; set; }
    public string LOT { get; set; } = null!;
    public string MLOT { get; set; } = null!;
    public decimal UNCONSUME_QTY { get; set; }
    public DateTime? REPORT_TIME { get; set; }
    public string ACCOUNT_NO { get; set; } = null!;
    public string? INPUT_FORM_NAME { get; set; }
    public string? COMMENT { get; set; }
}

public class MmsMLotStateChangeInputDto
{
    public decimal DATA_LINK_SID { get; set; }
    public string MLOT { get; set; } = null!;
    public string NEW_MLOT_STATE_CODE { get; set; } = null!;
    public string? REASON_CODE { get; set; }
    public DateTime? REPORT_TIME { get; set; }
    public string ACCOUNT_NO { get; set; } = null!;
    public string? INPUT_FORM_NAME { get; set; }
    public string? COMMENT { get; set; }
}
