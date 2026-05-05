namespace DcMateH5.Abstractions.Eqm.Models;

public class EqmStatusChangeInputDto
{
    public decimal DATA_LINK_SID { get; set; }
    public string EQM_NO { get; set; } = string.Empty;
    public string EQM_STATUS_NO { get; set; } = string.Empty;
    public string REASON_NO { get; set; } = string.Empty;
    public DateTime REPORT_TIME { get; set; }
    public string INPUT_FORM_NAME { get; set; } = string.Empty;
    public bool UPDATE_EQM_MASTER { get; set; }
}
