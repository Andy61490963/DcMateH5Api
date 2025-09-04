using DcMateH5Api.Areas.Form.Controllers;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DcMateH5Api.Tests.ApiControllerTest;

public class FormDesignerMasterDetailControllerTests
{
    private readonly Mock<IFormDesignerService> _serviceMock = new();

    private FormDesignerMasterDetailController CreateController()
        => new FormDesignerMasterDetailController(_serviceMock.Object);

    [Fact]
    public void SaveMasterDetailFormHeader_MissingIds_ReturnsBadRequest()
    {
        var controller = CreateController();
        var vm = new MasterDetailFormHeaderViewModel();

        var result = controller.SaveMasterDetailFormHeader(vm);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
