using System.Net;
using System.Reflection;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Infrastructure.Form.Form;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class FormTableValueFunctionServiceTests
{
    [Fact]
    public void ResolveTvfParametersOrThrow_RejectsEmptyRequestParameters()
    {
        var exception = InvokeResolveTvfParameters(
            CreateTvfParamConfigs(),
            new Dictionary<string, object?>());

        Assert.NotNull(exception);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("StartDate", exception.Message);
        Assert.Contains("EndDate", exception.Message);
    }

    [Fact]
    public void ResolveTvfParametersOrThrow_RejectsMissingRequiredParameter()
    {
        var exception = InvokeResolveTvfParameters(
            CreateTvfParamConfigs(),
            new Dictionary<string, object?>
            {
                ["StartDate"] = "2026-06-01"
            });

        Assert.NotNull(exception);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("EndDate", exception.Message);
    }

    [Fact]
    public void ResolveTvfParametersOrThrow_RejectsBlankRequiredParameter()
    {
        var exception = InvokeResolveTvfParameters(
            CreateTvfParamConfigs(),
            new Dictionary<string, object?>
            {
                ["StartDate"] = "2026-06-01",
                ["EndDate"] = " "
            });

        Assert.NotNull(exception);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("EndDate", exception.Message);
    }

    [Fact]
    public void ResolveTvfParametersOrThrow_AcceptsRequiredParameters()
    {
        var result = InvokeResolveTvfParametersSuccessfully(
            CreateTvfParamConfigs(),
            new Dictionary<string, object?>
            {
                ["StartDate"] = "2026-06-01",
                ["EndDate"] = "2026-06-30"
            });

        Assert.Equal("2026-06-01", result["StartDate"]);
        Assert.Equal("2026-06-30", result["EndDate"]);
    }

    [Fact]
    public void ResolveTvfParametersOrThrow_NormalizesAtPrefixedRequestKeys()
    {
        var result = InvokeResolveTvfParametersSuccessfully(
            CreateTvfParamConfigs(),
            new Dictionary<string, object?>
            {
                ["@StartDate"] = "2026-06-01",
                ["@EndDate"] = "2026-06-30"
            });

        Assert.True(result.ContainsKey("StartDate"));
        Assert.True(result.ContainsKey("EndDate"));
    }

    private static List<FormFieldConfigDto> CreateTvfParamConfigs()
    {
        return
        [
            new FormFieldConfigDto
            {
                COLUMN_NAME = "@StartDate",
                FIELD_ORDER = 1000,
                IS_TVF_QUERY_PARAMETER = true,
                DATA_TYPE = "date"
            },
            new FormFieldConfigDto
            {
                COLUMN_NAME = "@EndDate",
                FIELD_ORDER = 2000,
                IS_TVF_QUERY_PARAMETER = true,
                DATA_TYPE = "date"
            }
        ];
    }

    private static HttpStatusCodeException InvokeResolveTvfParameters(
        List<FormFieldConfigDto> fieldConfigs,
        Dictionary<string, object?>? tvfParameters)
    {
        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokeResolveTvfParametersSuccessfully(fieldConfigs, tvfParameters));

        return Assert.IsType<HttpStatusCodeException>(exception.InnerException);
    }

    private static Dictionary<string, object?> InvokeResolveTvfParametersSuccessfully(
        List<FormFieldConfigDto> fieldConfigs,
        Dictionary<string, object?>? tvfParameters)
    {
        var method = typeof(FormTableValueFunctionService).GetMethod(
            "ResolveTvfParametersOrThrow",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method.Invoke(null, [Guid.NewGuid(), fieldConfigs, tvfParameters]);
        return Assert.IsType<Dictionary<string, object?>>(result);
    }
}
