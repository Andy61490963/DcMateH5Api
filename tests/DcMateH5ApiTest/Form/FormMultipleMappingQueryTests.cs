using System.Reflection;
using System.Text.Json;
using Dapper;
using DcMateClassLibrary.Enums.Form;
using DcMateH5.Abstractions.Form.ViewModels;
using DcMateH5.Infrastructure.Form.Form;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class FormMultipleMappingQueryTests
{
    [Fact]
    public void MappingListQuery_DeserializesFrontendConditionPayload()
    {
        const string json = """
        {
            "BaseId": "master pk",
            "Type": 2,
            "Page": 1,
            "PageSize": 10,
            "OrderBySeqAscending": false,
            "DetailConditions": [
                {
                    "Column": "ITEM_CODE",
                    "ConditionType": 2,
                    "Value": "A001",
                    "Value2": null,
                    "Values": null,
                    "DataType": "string"
                }
            ],
            "MappingConditions": [
                {
                    "Column": "SEQ",
                    "ConditionType": 1,
                    "Value": "10",
                    "Value2": null,
                    "Values": null,
                    "DataType": "int"
                }
            ]
        }
        """;

        var query = JsonSerializer.Deserialize<MappingListQuery>(json);

        Assert.NotNull(query);
        Assert.Equal("master pk", query.BaseId);
        Assert.Equal(MappingListType.UnlinkedOnly, query.Type);
        Assert.Equal(1, query.Page);
        Assert.Equal(10, query.PageSize);
        Assert.False(query.OrderBySeqAscending);
        Assert.Equal(ConditionType.Like, query.DetailConditions![0].ConditionType);
        Assert.Equal(ConditionType.Equal, query.MappingConditions![0].ConditionType);
    }

    [Fact]
    public void BuildConditionWhere_BindsDetailAndMappingConditionsWithAliases()
    {
        var detailConditions = new[]
        {
            new FormQueryConditionViewModel
            {
                Column = "ITEM_CODE",
                ConditionType = ConditionType.Like,
                Value = "A001"
            }
        };

        var mappingConditions = new[]
        {
            new FormQueryConditionViewModel
            {
                Column = "SEQ",
                ConditionType = ConditionType.Equal,
                Value = "10"
            }
        };

        var detail = InvokeBuildConditionWhere(
            detailConditions,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ITEM_CODE"] = "nvarchar" },
            "d",
            "dWhere");

        var mapping = InvokeBuildConditionWhere(
            mappingConditions,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["SEQ"] = "int" },
            "m",
            "mWhere");

        Assert.Equal(" AND d.[ITEM_CODE] LIKE @dWhere0 ESCAPE '\\'", detail.WhereSql);
        Assert.Equal("%A001%", detail.Params.Get<string>("dWhere0"));
        Assert.Equal(" AND m.[SEQ] = @mWhere0", mapping.WhereSql);
        Assert.Equal(10L, mapping.Params.Get<long>("mWhere0"));
    }

    [Fact]
    public void BuildConditionWhere_RejectsUnknownColumn()
    {
        var conditions = new[]
        {
            new FormQueryConditionViewModel
            {
                Column = "BAD_COLUMN",
                ConditionType = ConditionType.Equal,
                Value = "A001"
            }
        };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeBuildConditionWhere(
                conditions,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ITEM_CODE"] = "nvarchar" },
                "d",
                "dWhere"));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void EnsureNoMappingConditionsForUnlinked_RejectsMappingConditions()
    {
        var conditions = new List<FormQueryConditionViewModel>
        {
            new()
            {
                Column = "SEQ",
                ConditionType = ConditionType.Equal,
                Value = "10"
            }
        };

        var method = typeof(FormMultipleMappingService).GetMethod(
            "EnsureNoMappingConditionsForUnlinked",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [conditions]));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    private static (string WhereSql, DynamicParameters Params) InvokeBuildConditionWhere(
        IEnumerable<FormQueryConditionViewModel> conditions,
        IReadOnlyDictionary<string, string> columnTypes,
        string tableAlias,
        string paramPrefix)
    {
        var method = typeof(FormMultipleMappingService).GetMethod(
            "BuildConditionWhere",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var result = method.Invoke(null, [conditions, columnTypes, tableAlias, paramPrefix]);
        var tuple = Assert.IsType<ValueTuple<string, DynamicParameters>>(result);

        return (tuple.Item1, tuple.Item2);
    }
}
