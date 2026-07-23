using System.Reflection;
using System.Text.Json;
using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Abstractions.Form.ViewModels;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class FormJsonContractTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = null
    };

    [Theory]
    [MemberData(nameof(FormContractTypes))]
    public void FormContracts_KeepPublicJsonPropertyNames(Type contractType, string[] expectedPropertyNames)
    {
        var actualPropertyNames = contractType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.Equal(expectedPropertyNames, actualPropertyNames);
    }

    public static IEnumerable<object[]> FormContractTypes()
    {
        yield return Contract<FormFieldViewModel>(
            "ID",
            "FORM_FIELD_MASTER_ID",
            "FORM_FIELD_DROPDOWN_ID",
            "IS_PK",
            "TableName",
            "IsNullable",
            "IS_TVF_QUERY_PARAMETER",
            "TVF_CURRENT_VALUE",
            "COLUMN_NAME",
            "DISPLAY_NAME",
            "DATA_TYPE",
            "IS_EDITABLE",
            "IS_REQUIRED",
            "IS_DISPLAYED",
            "IS_VALIDATION_RULE",
            "FIELD_ORDER",
            "CONTROL_TYPE",
            "CONTROL_TYPE_WHITELIST",
            "CAN_QUERY",
            "DETAIL_TO_RELATION_DEFAULT_COLUMN",
            "QUERY_DEFAULT_VALUE",
            "QUERY_COMPONENT",
            "QUERY_CONDITION",
            "QUERY_COMPONENT_TYPE_WHITELIST",
            "SchemaType");

        yield return Contract<FormSubmissionInputModel>(
            "BaseId",
            "Pk",
            "TargetTableToUpsert",
            "InputFields");

        yield return Contract<FormInputField>(
            "FieldConfigId",
            "ColumnName",
            "Value");

        yield return Contract<SubmitFormResponse>(
            "RowId",
            "IsInsert");

        yield return Contract<FormListResponseViewModel>(
            "FormMasterId",
            "FormName",
            "BaseId",
            "TotalPageSize",
            "Items");

        yield return Contract<FormListRowViewModel>(
            "Pk",
            "Fields");

        yield return Contract<DeleteWithGuardRequestViewModel>(
            "FormFieldMasterId",
            "pk",
            "Parameters");

        yield return Contract<DeleteWithGuardResultViewModel>(
            "IsValid",
            "CanDelete",
            "BlockedByRule",
            "ErrorMessage",
            "Deleted");

        yield return Contract<FormHeaderViewModel>(
            "ID",
            "FORM_NAME",
            "FORM_CODE",
            "FORM_DESCRIPTION",
            "BASE_TABLE_ID",
            "VIEW_TABLE_ID");

        yield return Contract<FormHeaderTableValueFunctionViewModel>(
            "ID",
            "FORM_NAME",
            "FORM_CODE",
            "FORM_DESCRIPTION",
            "TVF_TABLE_ID");

        yield return Contract<DropdownOptionItemViewModel>(
            "OptionText",
            "OptionValue",
            "OptionType");

        yield return Contract<FormFieldDropdownOptionsDto>(
            "ID",
            "FORM_FIELD_DROPDOWN_ID",
            "OPTION_TABLE",
            "OPTION_VALUE",
            "OPTION_TEXT",
            "OPTION_TYPE",
            "IS_DELETE");
    }

    [Fact]
    public void DropdownOptionItem_AcceptsCamelCaseOptionType()
    {
        const string json =
            """{"optionText":"Enabled","optionValue":"Y","optionType":"Status"}""";

        var option = JsonSerializer.Deserialize<DropdownOptionItemViewModel>(json, ApiJsonOptions);

        Assert.NotNull(option);
        Assert.Equal("Status", option.OptionType);
    }

    [Fact]
    public void DropdownOptionResponse_UsesUppercaseOptionType()
    {
        var json = JsonSerializer.Serialize(
            new FormFieldDropdownOptionsDto { OPTION_TYPE = "Status" },
            ApiJsonOptions);

        Assert.Contains("\"OPTION_TYPE\":\"Status\"", json);
        Assert.DoesNotContain("\"optionType\"", json);
    }

    private static object[] Contract<T>(params string[] propertyNames)
    {
        return new object[] { typeof(T), propertyNames };
    }
}
