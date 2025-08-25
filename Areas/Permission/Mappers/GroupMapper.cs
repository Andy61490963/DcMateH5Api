using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;

namespace DcMateH5Api.Areas.Permission.Mappers;

public class GroupMapper
{
    public static Group MapperGroupRequestAndDto(CreateGroupRequest dto)
    {
        return new Group
        {
            Id = Guid.NewGuid(),               
            Name = dto.Name,
            IsActive = true
        };
    }
}