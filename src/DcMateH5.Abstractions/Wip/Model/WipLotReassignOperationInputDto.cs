namespace DcMateH5Api.Areas.Wip.Model;

public class WipLotReassignOperationInputDto
{
    public string LOT { get; set; } = string.Empty;
    public decimal DATA_LINK_SID { get; set; }
    public int NEW_OPER_SEQ { get; set; }
    public DateTime? REPORT_TIME { get; set; }
    public string ACCOUNT_NO { get; set; } = string.Empty;
    public string? COMMENT { get; set; }
    public string? INPUT_FORM_NAME { get; set; }
}
