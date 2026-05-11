using System.Reflection;
using Dapper;
using DcMateClassLibrary.Enums.Form;
using DcMateH5.Abstractions.Form.ViewModels;
using DcMateH5.Infrastructure.Form.Form;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class FormQueryConditionTests
{
    [Theory]
    [InlineData(ConditionType.IsNull, " WHERE [CLOSE_TIME] IS NULL")]
    [InlineData(ConditionType.IsNotNull, " WHERE [CLOSE_TIME] IS NOT NULL")]
    public void BuildWhereClause_SupportsNullConditions(ConditionType conditionType, string expectedSql)
    {
        var parameters = new DynamicParameters();
        var conditions = new[]
        {
            new FormQueryConditionViewModel
            {
                Column = "CLOSE_TIME",
                ConditionType = conditionType,
                Value = "ignored",
                DataType = "datetime"
            }
        };

        var sql = InvokeBuildWhereClause(conditions, parameters);

        Assert.Equal(expectedSql, sql);
        Assert.Empty(parameters.ParameterNames);
    }

    private static string InvokeBuildWhereClause(
        IEnumerable<FormQueryConditionViewModel> conditions,
        DynamicParameters parameters)
    {
        var method = typeof(FormViewService).GetMethod(
            "BuildWhereClause",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, [conditions, parameters]));
    }
}
