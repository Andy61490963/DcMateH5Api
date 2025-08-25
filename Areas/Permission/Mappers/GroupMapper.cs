using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;

namespace DcMateH5Api.Areas.Permission.Mappers;

public class GroupMapper
{
    public static Group MapperCreate(CreateGroupRequest request)
    {
        return new Group
        {
            Id = Guid.NewGuid(),               
            Name = request.Name,
            IsActive = true
        };
    }
    
    public static Group MapperUpdate(Guid id, UpdateGroupRequest request)
    {
        return new Group
        {
            Id = id,               
            Name = request.Name,
            IsActive = request.IsActive,
            Description = request.Description
        };
    }
}