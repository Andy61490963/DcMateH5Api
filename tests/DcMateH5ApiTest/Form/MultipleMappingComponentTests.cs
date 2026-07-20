using System.Reflection;
using DcMateClassLibrary.Enums.Form;
using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Abstractions.Form.ViewModels;
using DcMateH5.Infrastructure.Form.Form;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class MultipleMappingComponentTests
{
    [Fact]
    public void NormalizeComponentOptions_TrimsAndOrdersStaticOptions()
    {
        var options = new List<MappingComponentOptionViewModel>
        {
            new() { Value = " B ", Text = " Beta ", Order = 2 },
            new() { Value = " A ", Text = " Alpha ", Order = 1 }
        };

        var normalized = InvokePrivateStatic<IReadOnlyList<MappingComponentOptionViewModel>>(
            "NormalizeComponentOptions",
            options);

        Assert.Collection(
            normalized,
            first =>
            {
                Assert.Equal("A", first.Value);
                Assert.Equal("Alpha", first.Text);
                Assert.Equal(1, first.Order);
            },
            second =>
            {
                Assert.Equal("B", second.Value);
                Assert.Equal("Beta", second.Text);
                Assert.Equal(2, second.Order);
            });
    }

    [Fact]
    public void NormalizeComponentOptions_RejectsDuplicateValuesIgnoringCase()
    {
        var options = new List<MappingComponentOptionViewModel>
        {
            new() { Value = "A", Text = "Alpha" },
            new() { Value = "a", Text = "Duplicate" }
        };

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateStatic<IReadOnlyList<MappingComponentOptionViewModel>>(
                "NormalizeComponentOptions",
                options));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void ResolveMappingRowId_UsesConfiguredMappingPkInsteadOfDetailPk()
    {
        var header = new FormFieldMasterDto
        {
            MAPPING_PK_COLUMN = "SID"
        };
        var mappingRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SID"] = 123.0m,
            ["DETAIL_SID"] = 456.0m
        };

        var mappingRowId = InvokePrivateStatic<string>(
            "ResolveMappingRowId",
            header,
            mappingRow);

        Assert.Equal("123", mappingRowId);
    }

    [Theory]
    [InlineData(FormControlType.Dropdown, true)]
    [InlineData(FormControlType.Radio, true)]
    [InlineData(FormControlType.Text, false)]
    [InlineData(FormControlType.Number, false)]
    public void IsOptionControl_OnlyAcceptsDropdownAndRadio(
        FormControlType controlType,
        bool expected)
    {
        Assert.Equal(
            expected,
            InvokePrivateStatic<bool>("IsOptionControl", controlType));
    }

    [Theory]
    [InlineData("SELECT CODE AS ID, NAME FROM LOOKUP", true)]
    [InlineData("SELECT CODE AS ID, NAME INTO TEMP_LOOKUP FROM LOOKUP", false)]
    [InlineData("SELECT CODE AS ID, NAME FROM LOOKUP; WAITFOR DELAY '00:00:01'", false)]
    [InlineData("UPDATE LOOKUP SET NAME = 'X'", false)]
    public void IsReadOnlyComponentOptionSql_RejectsWriteOrStackedStatements(
        string sql,
        bool expected)
    {
        Assert.Equal(
            expected,
            InvokePrivateStatic<bool>("IsReadOnlyComponentOptionSql", sql));
    }

    [Fact]
    public void UnconfiguredRuntimeComponent_DefaultsToNone()
    {
        var component = new MultipleMappingComponentViewModel();

        Assert.Equal(FormControlType.None, component.ControlType);
        Assert.False(component.IsConfigured);
        Assert.Empty(component.Options);
    }

    [Fact]
    public void MultipleMappingList_KeepsLegacyCollectionsAndAddsComponentDictionary()
    {
        var properties = typeof(MultipleMappingListViewModel)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Linked", properties);
        Assert.Contains("Unlinked", properties);
        Assert.Contains("ComponentsByMappingRowId", properties);
    }

    [Fact]
    public void RuntimeComponent_ExposesRequiredRenderingContract()
    {
        var properties = typeof(MultipleMappingComponentViewModel)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "MappingRowId",
                "DetailPk",
                "ControlType",
                "CurrentValue",
                "Options",
                "IsConfigured"
            },
            properties);
    }

    private static TResult InvokePrivateStatic<TResult>(string methodName, params object?[] arguments)
    {
        var method = typeof(FormMultipleMappingService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsAssignableFrom<TResult>(method.Invoke(null, arguments));
    }
}
