using DynamicForm.Areas.Form.Controllers;
using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DynamicForm.Tests.ApiControllerTest;

/// <summary>
/// 測試 <see cref="FormListController"/> 的 API 行為。
/// </summary>
public class FormListControllerTests
{
    private readonly Mock<IFormListService> _serviceMock;
    private readonly FormListController     _controller;

    public FormListControllerTests()
    {
        _serviceMock = new Mock<IFormListService>();
        _controller  = new FormListController(_serviceMock.Object);
    }

    [Fact]
    public void GetFormMasters_NoQuery_ReturnsAll()
    {
        var list = new List<FORM_FIELD_Master>
        {
            new() { FORM_NAME = "A" },
            new() { FORM_NAME = "B" }
        };
        _serviceMock.Setup(s => s.GetFormMasters()).Returns(list);

        var result = _controller.GetFormMasters(null) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(list, result.Value);
    }

    [Fact]
    public void GetFormMasters_WithQuery_FiltersResult()
    {
        var list = new List<FORM_FIELD_Master>
        {
            new() { FORM_NAME = "Target" },
            new() { FORM_NAME = "Other" }
        };
        _serviceMock.Setup(s => s.GetFormMasters()).Returns(list);

        var result = _controller.GetFormMasters("tar") as OkObjectResult;

        Assert.NotNull(result);
        var value = Assert.IsType<List<FORM_FIELD_Master>>(result.Value);
        Assert.Single(value);
        Assert.Equal("Target", value[0].FORM_NAME);
    }

    [Fact]
    public void Delete_ReturnsNoContentAndInvokesService()
    {
        var id = Guid.NewGuid();

        var result = _controller.Delete(id) as NoContentResult;

        _serviceMock.Verify(s => s.DeleteFormMaster(id), Times.Once);
        Assert.NotNull(result);
    }
}

