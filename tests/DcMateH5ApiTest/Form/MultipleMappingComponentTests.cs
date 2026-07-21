using System.Data;
using System.Reflection;
using Dapper;
using DcMateClassLibrary.Enums.Form;
using DcMateClassLibrary.Helper.FormHelper;
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
        Assert.Contains("TargetMappingColumnName", properties);
        Assert.Contains("MappingComponentTargetColumnName", properties);
        Assert.Contains("ComponentsByMappingRowId", properties);
    }

    [Fact]
    public void DesignerComponentList_ExposesSharedTargetColumnMetadata()
    {
        var properties = typeof(MappingComponentDesignerListViewModel)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("MappingComponentTargetColumnName", properties);
    }

    [Fact]
    public void FormHeader_SeparatesLegacyAndComponentTargetColumns()
    {
        var properties = typeof(MultipleMappingFormHeaderViewModel)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("TARGET_MAPPING_COLUMN_NAME", properties);
        Assert.Contains("MAPPING_COMPONENT_TARGET_COLUMN_NAME", properties);
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

    [Fact]
    public void MappingRowIdDbRow_MaterializesScalarMappingPk()
    {
        var rowType = typeof(FormMultipleMappingService).GetNestedType(
            "MappingRowIdDbRow",
            BindingFlags.NonPublic);
        Assert.NotNull(rowType);

        var table = new DataTable();
        table.Columns.Add("MappingRowId", typeof(decimal));
        table.Rows.Add(156202957262999m);

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        var row = reader.GetRowParser(rowType)(reader);
        var mappingRowId = rowType
            .GetProperty("MappingRowId", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(row);

        Assert.Equal(156202957262999m, mappingRowId);
    }

    [Fact]
    public void ValidateAndConvertComponentValue_RejectsNullOptionValue()
    {
        var options = new List<MappingComponentOptionViewModel>
        {
            new() { Value = "A", Text = "Alpha" }
        };

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateStatic<object>(
                "ValidateAndConvertComponentValue",
                FormControlType.Dropdown,
                options,
                "VALUE",
                "varchar",
                null));

        var innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("不可為 NULL", innerException.Message);
    }

    [Fact]
    public void ValidateAndConvertComponentValue_RejectsValueOutsideOptions()
    {
        var options = new List<MappingComponentOptionViewModel>
        {
            new() { Value = "A", Text = "Alpha" }
        };

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateStatic<object>(
                "ValidateAndConvertComponentValue",
                FormControlType.Dropdown,
                options,
                "VALUE",
                "varchar",
                "B"));

        var innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("有效選項", innerException.Message);
    }

    [Fact]
    public void ValidateAndConvertComponentValue_AcceptsConfiguredOption()
    {
        var options = new List<MappingComponentOptionViewModel>
        {
            new() { Value = "1", Text = "Enabled" }
        };

        var convertedValue = InvokePrivateStatic<object>(
            "ValidateAndConvertComponentValue",
            FormControlType.Dropdown,
            options,
            "ENABLED",
            "bit",
            true);

        Assert.Equal(true, convertedValue);
    }

    [Fact]
    public void TryConvertStrict_RejectsInvalidBitInsteadOfConvertingToFalse()
    {
        var success = ConvertToColumnTypeHelper.TryConvertStrict(
            "bit",
            "not-a-boolean",
            out var convertedValue);

        Assert.False(success);
        Assert.Null(convertedValue);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    public void TryConvertStrict_ConvertsSupportedBitValues(string value, bool expected)
    {
        var success = ConvertToColumnTypeHelper.TryConvertStrict(
            "bit",
            value,
            out var convertedValue);

        Assert.True(success);
        Assert.Equal(expected, convertedValue);
    }

    [Fact]
    public void TryConvertStrict_RejectsIntOverflow()
    {
        var success = ConvertToColumnTypeHelper.TryConvertStrict(
            "int",
            ((long)int.MaxValue + 1).ToString(),
            out var convertedValue);

        Assert.False(success);
        Assert.Null(convertedValue);
    }

    [Fact]
    public void TryConvertStrict_ConvertsGuidAndDatetime2()
    {
        var guid = Guid.NewGuid();

        Assert.True(ConvertToColumnTypeHelper.TryConvertStrict(
            "uniqueidentifier",
            guid.ToString(),
            out var convertedGuid));
        Assert.Equal(guid, convertedGuid);

        Assert.True(ConvertToColumnTypeHelper.TryConvertStrict(
            "datetime2",
            "2026-07-20T13:45:30.1234567",
            out var convertedDateTime));
        Assert.IsType<DateTime>(convertedDateTime);
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
