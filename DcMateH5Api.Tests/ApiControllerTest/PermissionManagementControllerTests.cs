using DcMateH5Api.Areas.Permission.Controllers;
using DcMateH5Api.Areas.Permission.Interfaces;
using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ClassLibrary;

namespace DcMateH5Api.Tests.ApiControllerTest;

/// <summary>
/// 測試 <see cref="PermissionManagementController"/> 在新增與更新時對於重複名稱的處理。
/// </summary>
public class PermissionManagementControllerTests
{
    private readonly Mock<IPermissionService> _service = new();

    private PermissionManagementController CreateController()
        => new(_service.Object);

    [Fact]
    public async Task CreateGroup_Duplicate_ReturnsConflict()
    {
        var request = new CreateGroupRequest { Name = "G" };
        _service.Setup(s => s.GroupNameExistsAsync("G", It.IsAny<CancellationToken>(), (Guid?)null)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.CreateGroup(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateGroup_Duplicate_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        var request = new UpdateGroupRequest { Name = "G" };
        _service.Setup(s => s.GetGroupAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Group { Id = id, Name = "Old" });
        _service.Setup(s => s.GroupNameExistsAsync("G", It.IsAny<CancellationToken>(), id)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.UpdateGroup(id, request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CreateFunction_Duplicate_ReturnsConflict()
    {
        var request = new CreateFunctionRequest { Name = "F", Area = "A", Controller = "C" };
        _service.Setup(s => s.FunctionNameExistsAsync("F", It.IsAny<CancellationToken>(), (Guid?)null)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.CreateFunction(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateFunction_Duplicate_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        var request = new UpdateFunctionRequest { Name = "F", Area = "A", Controller = "C" };
        _service.Setup(s => s.GetFunctionAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Function { Id = id, Name = "Old", Area = "A", Controller = "C" });
        _service.Setup(s => s.FunctionNameExistsAsync("F", It.IsAny<CancellationToken>(), id)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.UpdateFunction(id, request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CreateMenu_Duplicate_ReturnsConflict()
    {
        var request = new CreateMenuRequest { ParentId = null, SysFunctionId = Guid.NewGuid(), Name = "M", Sort = 1, IsShare = false };
        _service.Setup(s => s.MenuNameExistsAsync("M", null, It.IsAny<CancellationToken>(), (Guid?)null)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.CreateMenu(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateMenu_Duplicate_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        var request = new UpdateMenuRequest { ParentId = null, SysFunctionId = Guid.NewGuid(), Name = "M", Sort = 1, IsShare = false };
        _service.Setup(s => s.GetMenuAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Menu { Id = id, ParentId = null, SysFunctionId = request.SysFunctionId, Name = "Old", Sort = 1, IsShare = false });
        _service.Setup(s => s.MenuNameExistsAsync("M", null, It.IsAny<CancellationToken>(), id)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.UpdateMenu(id, request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CreatePermission_Duplicate_ReturnsConflict()
    {
        var request = new CreatePermissionRequest { Code = ActionType.View };
        _service.Setup(s => s.PermissionCodeExistsAsync(ActionType.View, It.IsAny<CancellationToken>(), (Guid?)null)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.CreatePermission(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdatePermission_Duplicate_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        var request = new UpdatePermissionRequest { Code = ActionType.View };
        _service.Setup(s => s.GetPermissionAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new PermissionModel { Id = id, Code = ActionType.View });
        _service.Setup(s => s.PermissionCodeExistsAsync(ActionType.View, It.IsAny<CancellationToken>(), id)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.UpdatePermission(id, request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }
}

