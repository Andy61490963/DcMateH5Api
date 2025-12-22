using System;
using System.Collections.Generic;
using System.Threading;
using DcMateH5Api.Areas.Form.Controllers;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DcMateH5Api.Tests.ApiControllerTest;

public class FormMultipleMappingControllerTests
{
    private readonly Mock<IFormMultipleMappingService> _service = new();

    private FormMultipleMappingController CreateController()
        => new(_service.Object);

    [Fact]
    public void GetForms_RequestIsNull_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.GetForms(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetForms_RequestIsValid_ReturnsOkWithResult()
    {
        var controller = CreateController();
        var request = new FormSearchRequest(Guid.NewGuid());
        var expected = new List<FormListDataViewModel> { new() };
        _service.Setup(s => s.GetForms(request, It.IsAny<CancellationToken>())).Returns(expected);

        var result = controller.GetForms(request, CancellationToken.None) as OkObjectResult;

        _service.Verify(s => s.GetForms(request, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public void GetMappingList_BaseIdMissing_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.GetMappingList(Guid.NewGuid(), string.Empty, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void AddMappings_ValidRequest_ReturnsNoContent()
    {
        var controller = CreateController();
        var formMasterId = Guid.NewGuid();
        var request = new MultipleMappingUpsertViewModel
        {
            BaseId = "1",
            DetailIds = new List<string> { "2" }
        };

        var result = controller.AddMappings(formMasterId, request, CancellationToken.None);

        _service.Verify(s => s.AddMappings(formMasterId, request, It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void RemoveMappings_ValidRequest_ReturnsNoContent()
    {
        var controller = CreateController();
        var formMasterId = Guid.NewGuid();
        var request = new MultipleMappingUpsertViewModel
        {
            BaseId = "B-1",
            DetailIds = new List<string> { "D-1", "D-2" }
        };

        var result = controller.RemoveMappings(formMasterId, request, CancellationToken.None);

        _service.Verify(s => s.RemoveMappings(formMasterId, request, It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void GetMappingTableData_FormMasterIdEmpty_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.GetMappingTableData(Guid.Empty, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetMappingTableData_ValidRequest_ReturnsOk()
    {
        var controller = CreateController();
        var formMasterId = Guid.NewGuid();
        var data = new MappingTableDataViewModel
        {
            FormMasterId = formMasterId,
            MappingTableName = "FORM_MAPPING",
            Rows = new List<MappingTableRowViewModel>()
        };
        _service.Setup(s => s.GetMappingTableData(formMasterId, It.IsAny<CancellationToken>())).Returns(data);

        var result = controller.GetMappingTableData(formMasterId, CancellationToken.None) as OkObjectResult;

        _service.Verify(s => s.GetMappingTableData(formMasterId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(result);
        Assert.Same(data, result.Value);
    }
}
