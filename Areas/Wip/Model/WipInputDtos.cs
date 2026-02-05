namespace DcMateH5Api.Areas.Wip.Model;

public class WipAddDetailInputDto
{
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public decimal OK_QTY { get; set; }
    public decimal NG_QTY { get; set; }
    public string? COMMENT { get; set; }
    public List<NgDetailItem>? NgDetails { get; set; } = new();
}

public class NgDetailItem
{
    public decimal NG_QTY { get; set; }
    public string NG_CODE { get; set; }
    public string Comment { get; set; }
}

public class WipCheckOutInputDto
{
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public DateTime CHECK_OUT_TIME { get; set; }
}

public class WipEditDetailInputDto
{
    public decimal WIP_OPI_WDOEACICO_HIST_DETAIL_SID { get; set; }
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public decimal OK_QTY { get; set; }
    public decimal NG_QTY { get; set; }
    public string? COMMENT { get; set; }
    public List<NgDetailItem>? NgDetails { get; set; } = new();
}
