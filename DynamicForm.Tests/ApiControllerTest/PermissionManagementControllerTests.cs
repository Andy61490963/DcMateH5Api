using DynamicForm.Areas.Permission.Controllers;
using DynamicForm.Areas.Permission.Interfaces;
using DynamicForm.Areas.Permission.Models;
using DynamicForm.Areas.Permission.ViewModels.PermissionManagement;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using System;
using ClassLibrary;

namespace DynamicForm.Tests.ApiControllerTest;

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
        _service.Setup(s => s.GroupNameExistsAsync("G", null)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.CreateGroup(request);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateGroup_Duplicate_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        var request = new UpdateGroupRequest { Name = "G" };
        _service.Setup(s => s.GetGroupAsync(id)).ReturnsAsync(new Group { Id = id, Name = "Old" });
        _service.Setup(s => s.GroupNameExistsAsync("G", id)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.UpdateGroup(id, request);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CreateFunction_Duplicate_ReturnsConflict()
    {
        var request = new CreateFunctionRequest { Name = "F", Area = "A", Controller = "C" };
        _service.Setup(s => s.FunctionNameExistsAsync("F", null)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.CreateFunction(request);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateFunction_Duplicate_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        var request = new UpdateFunctionRequest { Name = "F", Area = "A", Controller = "C" };
        _service.Setup(s => s.GetFunctionAsync(id)).ReturnsAsync(new Function { Id = id, Name = "Old", Area = "A", Controller = "C" });
        _service.Setup(s => s.FunctionNameExistsAsync("F", id)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.UpdateFunction(id, request);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CreateMenu_Duplicate_ReturnsConflict()
    {
        var request = new CreateMenuRequest { ParentId = null, SysFunctionId = Guid.NewGuid(), Name = "M", Sort = 1, IsShare = false };
        _service.Setup(s => s.MenuNameExistsAsync("M", null, null)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.CreateMenu(request);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateMenu_Duplicate_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        var request = new UpdateMenuRequest { ParentId = null, SysFunctionId = Guid.NewGuid(), Name = "M", Sort = 1, IsShare = false };
        _service.Setup(s => s.GetMenuAsync(id)).ReturnsAsync(new Menu { Id = id, ParentId = null, SysFunctionId = request.SysFunctionId, Name = "Old", Sort = 1, IsShare = false });
        _service.Setup(s => s.MenuNameExistsAsync("M", null, id)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.UpdateMenu(id, request);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CreatePermission_Duplicate_ReturnsConflict()
    {
        var request = new CreatePermissionRequest { Code = ActionType.View };
        _service.Setup(s => s.PermissionCodeExistsAsync(ActionType.View, null)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.CreatePermission(request);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdatePermission_Duplicate_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        var request = new UpdatePermissionRequest { Code = ActionType.View };
        _service.Setup(s => s.GetPermissionAsync(id)).ReturnsAsync(new PermissionModel { Id = id, Code = ActionType.View });
        _service.Setup(s => s.PermissionCodeExistsAsync(ActionType.View, id)).ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.UpdatePermission(id, request);

        Assert.IsType<ConflictObjectResult>(result);
    }
}

