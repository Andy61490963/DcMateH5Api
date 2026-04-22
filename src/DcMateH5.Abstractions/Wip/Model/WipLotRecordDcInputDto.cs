namespace DcMateH5Api.Areas.Wip.Model;

public class WipLotRecordDcInputDto
{
    public string ACTION_CODE { get; set; } = string.Empty;
    public string DC_TYPE { get; set; } = string.Empty;
    public string LOT { get; set; } = string.Empty;
    public decimal DATA_LINK_SID { get; set; }
    public string ACCOUNT_NO { get; set; } = string.Empty;
    public string? EQP_NO { get; set; }
    public decimal? SHIFT_SID { get; set; }
    public DateTime? REPORT_TIME { get; set; }
    public string? COMMENT { get; set; }
    public string? INPUT_FORM_NAME { get; set; }
    public List<WipLotRecordDcItemInputDto> ITEMS { get; set; } = [];
}

public class WipLotRecordDcItemInputDto
{
    public decimal? DC_ITEM_SID { get; set; }
    public string? DC_ITEM_CODE { get; set; }
    public string? DC_ITEM_NAME { get; set; }
    public decimal? DC_ITEM_SEQ { get; set; }
    public string? DATA_TYPE { get; set; }
    public string? DC_TYPE { get; set; }
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
