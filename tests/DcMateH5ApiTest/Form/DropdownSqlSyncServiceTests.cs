using System.Data;
using DcMateH5.Infrastructure.Form.Form;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class DropdownSqlSyncServiceTests
{
    [Fact]
    public void ReadSqlResult_MapsOptionalTypeIgnoringAliasCaseAndTrims()
    {
        var table = CreateOptionTable(includeType: true);
        table.Rows.Add("Y", "Enabled", "  Status  ");

        using var reader = table.CreateDataReader();
        var result = DropdownSqlSyncService.ReadSqlResult(reader);

        var option = Assert.Single(result.Rows);
        Assert.Equal("Y", option.OptionValue);
        Assert.Equal("Enabled", option.OptionText);
        Assert.Equal("Status", option.OptionType);
    }

    [Fact]
    public void ReadSqlResult_LeavesTypeNullWhenAliasIsMissing()
    {
        var table = CreateOptionTable(includeType: false);
        table.Rows.Add("Y", "Enabled");

        using var reader = table.CreateDataReader();
        var result = DropdownSqlSyncService.ReadSqlResult(reader);

        Assert.Null(Assert.Single(result.Rows).OptionType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadSqlResult_ConvertsEmptyTypeToNull(string? optionType)
    {
        var table = CreateOptionTable(includeType: true);
        table.Rows.Add("Y", "Enabled", optionType is null ? DBNull.Value : optionType);

        using var reader = table.CreateDataReader();
        var result = DropdownSqlSyncService.ReadSqlResult(reader);

        Assert.Null(Assert.Single(result.Rows).OptionType);
    }

    [Fact]
    public void ReadSqlResult_RejectsTypeLongerThan255Characters()
    {
        var table = CreateOptionTable(includeType: true);
        table.Rows.Add("Y", "Enabled", new string('T', 256));

        using var reader = table.CreateDataReader();

        Assert.Throws<DropdownSqlSyncException>(() => DropdownSqlSyncService.ReadSqlResult(reader));
    }

    private static DataTable CreateOptionTable(bool includeType)
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(string));
        table.Columns.Add("name", typeof(string));
        if (includeType)
        {
            table.Columns.Add("type", typeof(string));
        }

        return table;
    }
}
