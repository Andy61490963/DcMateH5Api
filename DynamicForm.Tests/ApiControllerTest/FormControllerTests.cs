using DynamicForm.Areas.Form.Controllers;
using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.Interfaces;
using DynamicForm.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;

namespace DynamicForm.Tests.ApiControllerTest;

/// <summary>
/// 測試 <see cref="FormController"/> 的 API 行為。
/// 使用 <see cref="Moq"/> 模擬服務層，以避免存取真實資料庫。
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
    public void GetForms_ReturnsOkWithViewModel()
    {
        var vm = new List<FormListDataViewModel> { new FormListDataViewModel { FormMasterId = Guid.NewGuid() } };
        _serviceMock.Setup(s => s.GetFormList(null)).Returns(vm);


        var result = _controller.GetForms(null) as OkObjectResult;

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
        var input = new FormSubmissionInputModel { FormId = Guid.NewGuid() };

        var result = _controller.SubmitForm(input) as NoContentResult;

        _serviceMock.Verify(s => s.SubmitForm(input), Times.Once);
        Assert.NotNull(result);
    }
}

