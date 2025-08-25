using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;

namespace DcMateH5Api.Areas.Permission.Mappers;

/// <summary>
/// 負責在 ViewModel 與資料庫模型之間轉換選單資料。
/// </summary>
public static class MenuMapper
{
    public static Menu MapperCreate(CreateMenuRequest request)
    {
        return new Menu
        {
            Id = Guid.NewGuid(),
            ParentId = request.ParentId,
            SysFunctionId = request.SysFunctionId,
            Name = request.Name,
            Sort = request.Sort,
            IsShare = request.IsShare,
            IsDelete = false
        };
    }

    public static Menu MapperUpdate(Guid id, UpdateMenuRequest request)
    {
        return new Menu
        {
            Id = id,
            ParentId = request.ParentId,
            SysFunctionId = request.SysFunctionId,
            Name = request.Name,
            Sort = request.Sort,
            IsShare = request.IsShare
        };
    }
}
