using System.Reflection;
using DcMateClassLibrary.Enums.Form;
using DcMateH5.Abstractions.Form.Form;
using DcMateH5.Abstractions.Form.ViewModels;
using DcMateH5Api.Areas.Form.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class FormControllerResponseContractTests
{
    [Fact]
    public void GetForms_NullBody_KeepsBadRequestWrapper()
    {
        var controller = new FormController(Proxy<IFormService>());

        var result = controller.GetForms(null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        AssertObjectProperties(badRequest.Value, "Error", "Hint");
    }

    [Fact]
    public async Task DeleteWithGuard_NullBody_KeepsBadRequestDetailWrapper()
    {
        var controller = new FormController(Proxy<IFormService>());

        var result = await controller.DeleteWithGuard(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        AssertObjectProperties(badRequest.Value, "Detail");
    }

    [Fact]
    public void SubmitForm_Insert_KeepsSubmitFormResponseShape()
    {
        var rowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var formService = Proxy<IFormService>((method, _) =>
        {
            if (method.Name == nameof(IFormService.SubmitForm))
            {
                return rowId;
            }

            throw new NotImplementedException(method.Name);
        });
        var controller = new FormController(formService);

        var result = controller.SubmitForm(new FormSubmissionInputModel
        {
            BaseId = Guid.NewGuid(),
            InputFields = new List<FormInputField>()
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SubmitFormResponse>(ok.Value);
        Assert.Equal(rowId.ToString(), response.RowId);
        Assert.True(response.IsInsert);
    }

    [Fact]
    public async Task SaveTvfHeader_KeepsOkIdWrapper()
    {
        var expectedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var tvfService = Proxy<IFormDesignerTableValueFunctionService>((method, _) =>
        {
            if (method.Name == nameof(IFormDesignerTableValueFunctionService.SaveTableValueFunctionFormHeader))
            {
                return Task.FromResult(expectedId);
            }

            throw new NotImplementedException(method.Name);
        });
        var controller = new FormDesignerTableValueFunctionController(
            Proxy<IFormDesignerService>(),
            tvfService);

        var result = await controller.SaveFormHeader(new FormHeaderTableValueFunctionViewModel
        {
            ID = Guid.NewGuid(),
            FORM_NAME = "TVF",
            FORM_CODE = "TVF",
            FORM_DESCRIPTION = "TVF",
            TVF_TABLE_ID = Guid.NewGuid()
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        AssertObjectProperties(ok.Value, "id");
        Assert.Equal(expectedId, GetPropertyValue<Guid>(ok.Value, "id"));
    }

    [Fact]
    public async Task SaveTvfHeader_EmptyTvfTableId_KeepsBadRequestString()
    {
        var controller = new FormDesignerTableValueFunctionController(
            Proxy<IFormDesignerService>(),
            Proxy<IFormDesignerTableValueFunctionService>());

        var result = await controller.SaveFormHeader(new FormHeaderTableValueFunctionViewModel
        {
            ID = Guid.NewGuid(),
            FORM_NAME = "TVF",
            FORM_CODE = "TVF",
            FORM_DESCRIPTION = "TVF",
            TVF_TABLE_ID = Guid.Empty
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<string>(badRequest.Value);
    }

    private static TService Proxy<TService>(
        Func<MethodInfo, object?[]?, object?>? handler = null)
        where TService : class
    {
        var proxy = DispatchProxy.Create<TService, TestDispatchProxy<TService>>();
        ((TestDispatchProxy<TService>)(object)proxy).Handler = handler;
        return proxy;
    }

    private static void AssertObjectProperties(object? value, params string[] expectedPropertyNames)
    {
        Assert.NotNull(value);
        var actualPropertyNames = value!
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.Equal(expectedPropertyNames, actualPropertyNames);
    }

    private static TValue GetPropertyValue<TValue>(object? value, string propertyName)
    {
        Assert.NotNull(value);
        var property = value!.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsType<TValue>(property.GetValue(value));
    }

    private class TestDispatchProxy<TService> : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                throw new MissingMethodException(typeof(TService).Name);
            }

            if (Handler != null)
            {
                return Handler(targetMethod, args);
            }

            throw new NotImplementedException(targetMethod.Name);
        }
    }
}
