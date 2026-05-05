using DcMateClassLibrary.Enums.Form;
using DcMateH5.Abstractions.Form.ViewModels;
using DcMateH5.Infrastructure.Form.Form;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class FormDesignerPureLogicTests
{
    [Fact]
    public void NormalizeAndValidateOptions_TrimsTextAndValue()
    {
        var result = FormDesignerPureLogic.NormalizeAndValidateOptions(
        [
            new DropdownOptionItemViewModel { OptionText = "  Enabled  ", OptionValue = "  Y  " },
            new DropdownOptionItemViewModel { OptionText = "Disabled", OptionValue = "N" }
        ]);

        Assert.Equal([("Enabled", "Y"), ("Disabled", "N")], result);
    }

    [Fact]
    public void NormalizeAndValidateOptions_RejectsBlankValues()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FormDesignerPureLogic.NormalizeAndValidateOptions(
            [
                new DropdownOptionItemViewModel { OptionText = "Enabled", OptionValue = "" }
            ]));
    }

    [Fact]
    public void NormalizeAndValidateOptions_RejectsDuplicateValuesIgnoringCase()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FormDesignerPureLogic.NormalizeAndValidateOptions(
            [
                new DropdownOptionItemViewModel { OptionText = "Enabled", OptionValue = "Y" },
                new DropdownOptionItemViewModel { OptionText = "Yes", OptionValue = "y" }
            ]));
    }

    [Theory]
    [InlineData("select * from dbo.TABLE")]
    [InlineData("  SELECT ID from FORM_FIELD_MASTER")]
    public void IsSelectSql_AllowsSelectStatements(string sql)
    {
        Assert.True(FormDesignerPureLogic.IsSelectSql(sql));
    }

    [Theory]
    [InlineData("update dbo.TABLE set NAME = 'x'")]
    [InlineData("select * from dbo.TABLE; delete from dbo.TABLE")]
    [InlineData("exec dbo.GetData")]
    public void IsSelectSql_RejectsMutatingStatements(string sql)
    {
        Assert.False(FormDesignerPureLogic.IsSelectSql(sql));
    }

    [Theory]
    [InlineData("FORM_FIELD_MASTER")]
    [InlineData("dbo.FORM_FIELD_MASTER")]
    public void ValidateTableName_AllowsSafeIdentifiers(string tableName)
    {
        FormDesignerPureLogic.ValidateTableName(tableName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dbo.FORM_FIELD_MASTER;DROP TABLE X")]
    [InlineData("dbo.FORM-FIELD")]
    public void ValidateTableName_RejectsUnsafeIdentifiers(string tableName)
    {
        Assert.Throws<InvalidOperationException>(() => FormDesignerPureLogic.ValidateTableName(tableName));
    }

    [Fact]
    public void CreateDefaultFieldConfig_PopulatesExpectedDefaults()
    {
        var masterId = Guid.NewGuid();
        var field = FormDesignerPureLogic.CreateDefaultFieldConfig(
            "EQP_ID",
            "nvarchar",
            sourceIsNullable: false,
            isTvfQueryParameter: true,
            masterId,
            "dbo.EQP",
            3,
            TableSchemaQueryType.OnlyTable);

        Assert.NotEqual(Guid.Empty, field.ID);
        Assert.Equal(masterId, field.FORM_FIELD_MASTER_ID);
        Assert.Equal("dbo.EQP", field.TableName);
        Assert.Equal("EQP_ID", field.COLUMN_NAME);
        Assert.Equal("nvarchar", field.DATA_TYPE);
        Assert.False(field.IS_REQUIRED);
        Assert.True(field.IS_EDITABLE);
        Assert.True(field.IS_DISPLAYED);
        Assert.Equal(3, field.FIELD_ORDER);
        Assert.Equal(QueryComponentType.None, field.QUERY_COMPONENT);
        Assert.Equal(ConditionType.Like, field.QUERY_CONDITION);
        Assert.False(field.CAN_QUERY);
    }
}
