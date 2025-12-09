namespace DcMateH5Api.Areas.RouteOperation.ViewModels;

public class ReorderRouteOperationsRequest
{
    /// <summary>
    /// 重新排序後的 RouteOperation SID 列表（順序即為新的 SEQ 順序）。
    /// </summary>
    public List<decimal> OrderedRouteOperationSids { get; set; } = new();
}
