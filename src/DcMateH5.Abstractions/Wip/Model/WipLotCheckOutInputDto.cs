namespace DcMateH5Api.Areas.Wip.Model;

public class WipLotCheckOutInputDto
{
    public string LOT { get; set; } = null!;
    public decimal DATA_LINK_SID { get; set; }
    public DateTime? REPORT_TIME { get; set; }
    public string ACCOUNT_NO { get; set; } = null!;
    public string? EQP_NO { get; set; }
    public decimal? SHIFT_SID { get; set; }
    public bool GROUP_IN_USER { get; set; }
    public string? COMMENT { get; set; }
    public string? INPUT_FORM_NAME { get; set; }
}
