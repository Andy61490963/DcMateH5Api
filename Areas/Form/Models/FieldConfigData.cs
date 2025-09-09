namespace DcMateH5Api.Areas.Form.Models;

public sealed record FieldConfigData(
    List<FormFieldConfigDto> FieldConfigs,
    List<FormFieldValidationRuleDto> ValidationRules,
    List<FormDropDownDto> DropdownConfigs,
    List<FormFieldDropdownOptions> DropdownOptions);

