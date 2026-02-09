namespace DcMateH5Api.Areas.Wip.Model;

public class WipCheckInAddDetailsCheckOutInputDto
{
    public WipCheckInInputDto CheckIn { get; set; } = new();
    public WipAddDetailForCombineInputDto AddDetails { get; set; } = new();
    public DateTime CHECK_OUT_TIME { get; set; }
}

public class WipAddDetailForCombineInputDto
{
    public decimal OK_QTY { get; set; }
    public decimal NG_QTY { get; set; }
    public string? COMMENT { get; set; }
    public List<NgDetailItem>? NgDetails { get; set; } = new();
}