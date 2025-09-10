using ClassLibrary;
using DcMateH5Api.Areas.Form.Controllers;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Areas.Form.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;

namespace DcMateH5Api.Tests.ApiControllerTest;

/// <summary>
/// 測試 <see cref="FormController"/> 的 API 行為。
/// </summary>
public class FormControllerTests
{
    private readonly Mock<IFormService> _serviceMock;
    private readonly FormController     _controller;

    public FormControllerTests()
    {
        _serviceMock = new Mock<IFormService>();
        _controller  = new FormController(_serviceMock.Object);
    }

    [Fact]
    public void GetForms_NullRequest_ReturnsBadRequest()
    {
        var result = _controller.GetForms(null);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetForms_WithRequest_ReturnsOk()
    {
        var request = new FormSearchRequest(Guid.NewGuid());
        var vm = new List<FormListDataViewModel>();
        _serviceMock.Setup(s => s.GetFormList(FormFunctionType.NotMasterDetail, request)).Returns(vm);

        var result = _controller.GetForms(request) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(vm, result.Value);
    }

    [Fact]
    public void GetForm_WithRowId_CallsServiceWithId()
    {
        var formId = Guid.NewGuid();
        var rowId  = "row1";
        var vm     = new FormSubmissionViewModel { FormId = formId };
        _serviceMock.Setup(s => s.GetFormSubmission(formId, rowId)).Returns(vm);

        var result = _controller.GetForm(formId, rowId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(vm, result.Value);
    }

    [Fact]
    public void GetForm_WithoutRowId_CallsServiceWithoutId()
    {
        var formId = Guid.NewGuid();
        var vm     = new FormSubmissionViewModel { FormId = formId };
        _serviceMock.Setup(s => s.GetFormSubmission(formId, null)).Returns(vm);

        var result = _controller.GetForm(formId, null) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(vm, result.Value);
    }

    [Fact]
    public void SubmitForm_ReturnsNoContentAndInvokesService()
    {
        var input = new FormSubmissionInputModel { BaseId = Guid.NewGuid() };

        var result = _controller.SubmitForm(input) as NoContentResult;

        _serviceMock.Verify(s => s.SubmitForm(input), Times.Once);
        Assert.NotNull(result);
    }
}
