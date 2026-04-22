namespace DcMateH5Api.Areas.Wip.Model;

public class WipCreateLotInputDto
{
    public decimal DATA_LINK_SID { get; set; }
    public string LOT { get; set; } = null!;
    public string? ALIAS_LOT1 { get; set; }
    public string? ALIAS_LOT2 { get; set; }
    public string WO { get; set; } = null!;
    public decimal ROUTE_SID { get; set; }
    public decimal LOT_QTY { get; set; }
    public DateTime REPORT_TIME { get; set; }
    public string ACCOUNT_NO { get; set; } = null!;
    public string? INPUT_FORM_NAME { get; set; }
    public string? COMMENT { get; set; }
}
