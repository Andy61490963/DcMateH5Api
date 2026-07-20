using DcMateClassLibrary.Enums.Form;
using DcMateClassLibrary.Helper.Enums;
using DcMateClassLibrary.Helper.FormHelper;
using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Abstractions.Form.ViewModels;
using DcMateH5.Infrastructure.Form.FormLogic;
using Microsoft.Data.SqlClient;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class FormControlTypeTests
{
    [Fact]
    public void Radio_KeepsStableContractValue()
    {
        Assert.Equal(8, (int)FormControlType.Radio);
    }

    [Theory]
    [InlineData("bit")]
    [InlineData("int")]
    [InlineData("decimal")]
    [InlineData("nvarchar")]
    [InlineData("varchar")]
    [InlineData("char")]
    [InlineData("text")]
    [InlineData("date")]
    [InlineData("datetime")]
    [InlineData("uniqueidentifier")]
    [InlineData("unknown")]
    public void GetControlTypeWhitelist_IncludesRadioForOptionBasedFields(string dataType)
    {
        var whitelist = FormFieldHelper.GetControlTypeWhitelist(dataType);

        Assert.Contains(FormControlType.Radio, whitelist);
    }

    [Fact]
    public void Radio_ExposesExpectedEnumMetadata()
    {
        var option = EnumExtensions.ToDescriptionList(typeof(FormControlType))
            .Single(item => item.Key == nameof(FormControlType.Radio));

        Assert.Equal(8, option.Value);
        Assert.Equal("單選按鈕", option.Text);
        Assert.Equal("input type=radio", option.Description);
    }

    [Fact]
    public void ReplaceDropdownIdsWithTexts_MapsRadioOptionToDisplayText()
    {
        var fieldId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var rows = new List<FormDataRow>
        {
            new()
            {
                PkId = "ROW-1",
                Cells = [new FormDataCell { ColumnName = "STATUS", Value = optionId }]
            }
        };
        var fields = new List<FormFieldConfigDto>
        {
            new()
            {
                ID = fieldId,
                COLUMN_NAME = "STATUS",
                CONTROL_TYPE = FormControlType.Radio
            }
        };
        var answers = new List<DropdownAnswerDto>
        {
            new() { RowId = "ROW-1", FieldId = fieldId, OptionId = optionId }
        };

        using var connection = new SqlConnection();
        var service = new DropdownService(connection);
        service.ReplaceDropdownIdsWithTexts(
            rows,
            fields,
            answers,
            new Dictionary<Guid, string> { [optionId] = "啟用" });

        Assert.Equal("啟用", rows[0].Cells[0].Value);
    }
}
