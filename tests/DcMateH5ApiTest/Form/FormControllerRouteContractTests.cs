using System.Reflection;
using DcMateH5Api.Areas.Form.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class FormControllerRouteContractTests
{
    private static readonly Type[] FormControllerTypes =
    [
        typeof(FormController),
        typeof(FormDesignerController),
        typeof(FormDesignerMasterDetailController),
        typeof(FormDesignerMultipleMappingController),
        typeof(FormDesignerTableValueFunctionController),
        typeof(FormMasterDetailController),
        typeof(FormMultipleMappingController),
        typeof(FormTableValueFunctionController),
        typeof(FormViewController),
        typeof(FormViewDesignerController)
    ];

    [Theory]
    [MemberData(nameof(FormControllers))]
    public void FormControllers_KeepAreaAndControllerRoute(Type controllerType)
    {
        var area = controllerType.GetCustomAttribute<AreaAttribute>();
        var route = controllerType.GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(area);
        Assert.Equal("Form", area.RouteValue);
        Assert.NotNull(route);
        Assert.Equal("[area]/[controller]", route.Template);
    }

    [Theory]
    [InlineData(typeof(FormController), 4)]
    [InlineData(typeof(FormDesignerController), 27)]
    [InlineData(typeof(FormDesignerMasterDetailController), 20)]
    [InlineData(typeof(FormDesignerMultipleMappingController), 15)]
    [InlineData(typeof(FormDesignerTableValueFunctionController), 12)]
    [InlineData(typeof(FormMasterDetailController), 3)]
    [InlineData(typeof(FormMultipleMappingController), 8)]
    [InlineData(typeof(FormTableValueFunctionController), 2)]
    [InlineData(typeof(FormViewController), 2)]
    [InlineData(typeof(FormViewDesignerController), 10)]
    public void FormControllers_KeepActionRouteCount(Type controllerType, int expectedActionCount)
    {
        Assert.Equal(expectedActionCount, GetActionRoutes(controllerType).Count);
    }

    [Theory]
    [InlineData(typeof(FormDesignerController), "GetFormMasters", "GET", null)]
    [InlineData(typeof(FormDesignerController), "UpdateFormName", "PUT", "form-name")]
    [InlineData(typeof(FormDesignerController), "GetDesigner", "GET", "{id:guid}")]
    [InlineData(typeof(FormDesignerController), "SaveFormHeader", "POST", "headers")]
    [InlineData(typeof(FormDesignerMultipleMappingController), "GetDesigner", "GET", "{id:guid}")]
    [InlineData(typeof(FormDesignerTableValueFunctionController), "GetFields", "GET", "tables/{tvfName}/fields")]
    [InlineData(typeof(FormViewDesignerController), "GetFields", "GET", "tables/{viewName}/fields")]
    [InlineData(typeof(FormViewController), "GetFormMasters", "GET", "masters")]
    [InlineData(typeof(FormController), "DeleteWithGuard", "POST", "delete")]
    public void CriticalFormEndpoints_KeepHttpMethodAndTemplate(
        Type controllerType,
        string actionName,
        string expectedVerb,
        string? expectedTemplate)
    {
        var route = GetActionRoutes(controllerType)
            .Single(x => x.ActionName == actionName);

        Assert.Equal(expectedVerb, route.Verb);
        Assert.Equal(expectedTemplate, route.Template);
    }

    public static IEnumerable<object[]> FormControllers()
    {
        return FormControllerTypes.Select(type => new object[] { type });
    }

    private static List<ActionRoute> GetActionRoutes(Type controllerType)
    {
        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method
                .GetCustomAttributes<HttpMethodAttribute>()
                .SelectMany(attribute => attribute.HttpMethods.Select(verb =>
                    new ActionRoute(method.Name, verb, attribute.Template))))
            .ToList();
    }

    private sealed record ActionRoute(string ActionName, string Verb, string? Template);
}
