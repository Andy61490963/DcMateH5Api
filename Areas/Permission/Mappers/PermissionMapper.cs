using ClassLibrary;
using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;

namespace DcMateH5Api.Areas.Permission.Mappers;

public class PermissionMapper
{
    public static PermissionModel MapperCreate(CreatePermissionRequest request)
    {
        return new PermissionModel
        {
            Id = Guid.NewGuid(),               
            Code = request.Code,
            IsActive = true
        };
    }
    
    public static PermissionModel MapperUpdate(Guid id, UpdatePermissionRequest request)
    {
        return new PermissionModel
        {
            Id = id,               
            Code = request.Code,
            IsActive = request.IsActive
        };
    }
}