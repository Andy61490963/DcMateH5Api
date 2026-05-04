namespace DcMateH5Api.Areas.Wip.Model;

public class WipCheckInAddDetailsCheckOutInputDto
{
    public WipCheckInInputDto CheckIn { get; set; } = new();
    public WipAddDetailForCombineInputDto AddDetails { get; set; } = new();
    public WipCheckInAddDetailsCheckOutDcInputDto? Dc { get; set; }
    public DateTime CHECK_OUT_TIME { get; set; }
}

public class WipAddDetailForCombineInputDto
{
    public decimal OK_QTY { get; set; }
    public decimal NG_QTY { get; set; }
    public string? COMMENT { get; set; }
    public List<NgDetailItem>? NgDetails { get; set; } = new();
}

public class WipCheckInAddDetailsCheckOutDcInputDto
{
    public WipOpiWdoeacicoHistDcItemInputDto? Item { get; set; }
    public List<WipOpiWdoeacicoHistDcItemInputDto>? Items { get; set; } = new();
}

public class WipOpiWdoeacicoHistDcItemInputDto
{
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
}
