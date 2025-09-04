using ClassLibrary;
using DcMateH5Api.Areas.Form.Controllers;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;
using DcMateH5Api.Helper;
using System.Net;
using System.Threading;

namespace DcMateH5Api.Tests.ApiControllerTest;

/// <summary>
/// 測試 <see cref="FormDesignerController"/> 主要的 API 行為。
/// 透過模擬的服務層確保 Controller 的邏輯正確。
/// </summary>
public class FormDesignerControllerTests
{
    private readonly Mock<IFormDesignerService> _designerMock = new();

    private FormDesignerController CreateController()
        => new FormDesignerController(_designerMock.Object);

    [Fact]
    public async Task GetDesigner_ReturnsViewModel()
    {
        var id = Guid.NewGuid();
        var vm = new FormDesignerIndexViewModel();
        _designerMock
            .Setup(s => s.GetFormDesignerIndexViewModel(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vm);
        var controller = CreateController();

        var result = await controller.GetDesigner(id, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(vm, result.Value);
    }

    [Fact]
    public void SetAllEditable_NotOnlyTable_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.BatchSetEditable(Guid.NewGuid(), "t", true, TableSchemaQueryType.All);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void SetAllEditable_OnlyTable_ReturnsUpdatedFields()
    {
        var controller = CreateController();
        var formId = Guid.NewGuid();
        var fields = new FormFieldListViewModel();
        _designerMock.Setup(s => s.GetFieldsByTableName("t", formId, TableSchemaQueryType.OnlyTable)).Returns(fields);

        var result = controller.BatchSetEditable(formId, "t", true, TableSchemaQueryType.OnlyTable) as OkObjectResult;

        _designerMock.Verify(s => s.SetAllEditable(formId, "t", true), Times.Once);
        Assert.NotNull(result);
        var model = Assert.IsType<FormFieldListViewModel>(result.Value);
        Assert.Same(fields, model);
        Assert.Equal(formId, model.ID);
        Assert.Equal(TableSchemaQueryType.OnlyTable, model.SchemaQueryType);
    }

    [Fact]
    public void SetAllRequired_NotOnlyTable_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.BatchSetRequired(Guid.NewGuid(), "t", true, TableSchemaQueryType.All);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void SetAllRequired_OnlyTable_ReturnsUpdatedFields()
    {
        var controller = CreateController();
        var formId = Guid.NewGuid();
        var fields = new FormFieldListViewModel();
        _designerMock.Setup(s => s.GetFieldsByTableName("t", formId, TableSchemaQueryType.OnlyTable)).Returns(fields);

        var result = controller.BatchSetRequired(formId, "t", true, TableSchemaQueryType.OnlyTable) as OkObjectResult;

        _designerMock.Verify(s => s.SetAllRequired(formId, "t", true), Times.Once);
        Assert.NotNull(result);
        var model = Assert.IsType<FormFieldListViewModel>(result.Value);
        Assert.Same(fields, model);
        Assert.Equal(formId, model.ID);
        Assert.Equal(TableSchemaQueryType.OnlyTable, model.SchemaQueryType);
    }

    [Fact]
    public void SaveFormHeader_MissingNames_ReturnsBadRequest()
    {
        var controller = CreateController();
        var vm = new FormHeaderViewModel();

        var result = controller.SaveFormHeader(vm);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void SaveMasterDetailFormHeader_MissingIds_ReturnsBadRequest()
    {
        var controller = CreateController();
        var vm = new MasterDetailFormHeaderViewModel();

        var result = controller.SaveMasterDetailFormHeader(vm);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetFields_MissingSystemColumns_ReturnsBadRequest()
    {
        var controller = CreateController();
        _designerMock
            .Setup(s => s.EnsureFieldsSaved("T", null, TableSchemaQueryType.OnlyTable))
            .Throws(new HttpStatusCodeException(HttpStatusCode.BadRequest, "缺少必要欄位"));

        var result = controller.GetFields("T", null, TableSchemaQueryType.OnlyTable);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, obj.StatusCode);
        Assert.Equal("缺少必要欄位", obj.Value);
    }

    [Fact]
    public async Task UpdateFormName_ReturnsOk()
    {
        var controller = CreateController();
        var vm = new UpdateFormNameViewModel { Id = Guid.NewGuid(), FormName = "N" };
        _designerMock
            .Setup(s => s.UpdateFormName(vm.Id, vm.FormName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await controller.UpdateFormName(vm, CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void GetField_ById_ReturnsField()
    {
        var controller = CreateController();
        var fieldId = Guid.NewGuid();
        var field = new FormFieldViewModel { ID = fieldId };
        _designerMock.Setup(s => s.GetFieldById(fieldId)).Returns(field);

        var result = controller.GetField(fieldId) as OkObjectResult;

        Assert.NotNull(result);
        var model = Assert.IsType<FormFieldViewModel>(result.Value);
        Assert.Equal(TableSchemaQueryType.OnlyTable, model.SchemaType);
    }

    [Fact]
    public void GetField_ById_NotFound()
    {
        var controller = CreateController();
        var fieldId = Guid.NewGuid();
        _designerMock.Setup(s => s.GetFieldById(fieldId)).Returns((FormFieldViewModel?)null);

        var result = controller.GetField(fieldId);

        Assert.IsType<NotFoundResult>(result);
    }
}

