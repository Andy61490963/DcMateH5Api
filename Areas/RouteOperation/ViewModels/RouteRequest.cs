namespace DcMateH5Api.Areas.RouteOperation.ViewModels;

public class RouteViewModel
{
    public decimal Sid { get; set; }
    public string RouteCode { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
}

public class CreateRouteRequest
{
    public string RouteCode { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
}

public class UpdateRouteRequest
{
    public string? RouteCode { get; set; }
    public string? RouteName { get; set; }
}