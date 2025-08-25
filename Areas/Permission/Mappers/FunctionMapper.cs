using ClassLibrary;
using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;

namespace DcMateH5Api.Areas.Permission.Mappers;

public class FunctionMapper
{
    public static Function MapperCreate(CreateFunctionRequest request)
    {
        return new Function
        {
            Id = Guid.NewGuid(),               
            Name = request.Name,
            Area = request.Area,
            Controller = request.Controller,
            DefaultEndpoint = request.DefaultEndpoint
        };
    }
    
    public static Function MapperUpdate(Guid id, UpdateFunctionRequest request)
    {
        return new Function
        {
            Id = id,               
            Name = request.Name,
            Area = request.Area,
            Controller = request.Controller,
            DefaultEndpoint = request.DefaultEndpoint
        };
    }
}