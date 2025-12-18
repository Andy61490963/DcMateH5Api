using System;
using DcMateH5Api.Areas.Form.Controllers;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Threading.Tasks;

namespace DcMateH5Api.Tests.ApiControllerTest;

public class FormDesignerMultipleMappingControllerTests
{
    private readonly Mock<IFormDesignerService> _serviceMock = new();

    private FormDesignerMultipleMappingController CreateController()
        => new FormDesignerMultipleMappingController(_serviceMock.Object);

    [Fact]
    public async Task SaveMultipleMappingFormHeader_MissingIds_ReturnsBadRequest()
    {
        var controller = CreateController();
        var vm = new MultipleMappingFormHeaderViewModel();

        var result = await controller.SaveMultipleMappingFormHeader(vm);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SaveMultipleMappingFormHeader_MissingMappingColumns_ReturnsBadRequest()
    {
        var controller = CreateController();
        var vm = new MultipleMappingFormHeaderViewModel
        {
            BASE_TABLE_ID = Guid.NewGuid(),
            DETAIL_TABLE_ID = Guid.NewGuid(),
            MAPPING_TABLE_ID = Guid.NewGuid(),
            FORM_NAME = "RouteOperation"
        };

        var result = await controller.SaveMultipleMappingFormHeader(vm);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
