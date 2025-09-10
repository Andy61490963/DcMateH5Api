using ClassLibrary;
using DcMateH5Api.Areas.Form.Controllers;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Threading;

namespace DcMateH5Api.Tests.ApiControllerTest;

/// <summary>
/// 測試 <see cref="FormDesignerController"/> 主要的 API 行為。
/// </summary>
public class FormDesignerControllerTests
{
    private readonly Mock<IFormDesignerService> _designerMock = new();

    private FormDesignerController CreateController()
        => new FormDesignerController(_designerMock.Object);

    [Fact]
    public async Task BatchSetEditable_ReturnsUpdatedFields()
    {
        var controller = CreateController();
        var formId = Guid.NewGuid();
        var fields = new FormFieldListViewModel();
        _designerMock.Setup(s => s.SetAllEditable(formId, true, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("t");
        _designerMock.Setup(s => s.GetFieldsByTableName("t", formId, TableSchemaQueryType.OnlyTable))
                     .ReturnsAsync(fields);

        var result = await controller.BatchSetEditable(formId, true, CancellationToken.None) as OkObjectResult;

        _designerMock.Verify(s => s.SetAllEditable(formId, true, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(result);
        Assert.Same(fields, result.Value);
    }

    [Fact]
    public async Task BatchSetRequired_ReturnsUpdatedFields()
    {
        var controller = CreateController();
        var formId = Guid.NewGuid();
        var fields = new FormFieldListViewModel();
        _designerMock.Setup(s => s.SetAllRequired(formId, true, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("t");
        _designerMock.Setup(s => s.GetFieldsByTableName("t", formId, TableSchemaQueryType.OnlyTable))
                     .ReturnsAsync(fields);

        var result = await controller.BatchSetRequired(formId, true, CancellationToken.None) as OkObjectResult;

        _designerMock.Verify(s => s.SetAllRequired(formId, true, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(result);
        Assert.Same(fields, result.Value);
    }

    [Fact]
    public async Task GetField_ById_ReturnsField()
    {
        var controller = CreateController();
        var fieldId = Guid.NewGuid();
        var field = new FormFieldViewModel();
        _designerMock.Setup(s => s.GetFieldById(fieldId)).ReturnsAsync(field);

        var result = await controller.GetField(fieldId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Same(field, result.Value);
    }

    [Fact]
    public async Task GetField_ById_NotFound()
    {
        var controller = CreateController();
        var fieldId = Guid.NewGuid();
        _designerMock.Setup(s => s.GetFieldById(fieldId)).ReturnsAsync((FormFieldViewModel?)null);

        var result = await controller.GetField(fieldId);

        Assert.IsType<NotFoundResult>(result);
    }
}
