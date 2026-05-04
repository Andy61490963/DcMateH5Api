namespace DcMateH5Api.Areas.Wip.Model;

public class WipOpiWdoeacicoHistDcInputDto
{
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public WipOpiWdoeacicoHistDcItemInputDto? Item { get; set; }
    public List<WipOpiWdoeacicoHistDcItemInputDto>? Items { get; set; } = new();
}
