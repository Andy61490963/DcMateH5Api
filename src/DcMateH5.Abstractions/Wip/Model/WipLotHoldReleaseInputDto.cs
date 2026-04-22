namespace DcMateH5Api.Areas.Wip.Model;

public class WipLotHoldReleaseInputDto
{
    public string LOT { get; set; } = null!;
    public decimal LOT_HOLD_SID { get; set; }
    public decimal REASON_SID { get; set; }
    public decimal DATA_LINK_SID { get; set; }
    public DateTime? REPORT_TIME { get; set; }
    public string ACCOUNT_NO { get; set; } = null!;
    public string? COMMENT { get; set; }
    public string? INPUT_FORM_NAME { get; set; }
}
