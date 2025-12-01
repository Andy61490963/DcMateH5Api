using DcMateH5Api.Areas.RouteOperation.Models;
using DcMateH5Api.Areas.RouteOperation.ViewModels;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.RouteOperation.Mappers;

public class RouteMapper
{
    public static BAS_ROUTE MapperCreate(CreateRouteRequest request)
    {
        return new BAS_ROUTE
        {
            SID = RandomHelper.GenerateRandomDecimal(), 
            ROUTE_CODE = request.RouteCode,
            ROUTE_NAME = request.RouteName
        };
    }

    public static void MapperUpdate(BAS_ROUTE entity, UpdateRouteRequest request)
    {
        if (request.RouteCode != null)
            entity.ROUTE_CODE = request.RouteCode;

        if (request.RouteName != null)
            entity.ROUTE_NAME = request.RouteName;
    }

    public static RouteViewModel ToViewModel(BAS_ROUTE entity)
    {
        return new RouteViewModel
        {
            Sid = entity.SID,
            RouteCode = entity.ROUTE_CODE,
            RouteName = entity.ROUTE_NAME
        };
    }
}