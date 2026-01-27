namespace DcMateH5Api.Areas.Form.Models;

/// <summary>
/// Dto 組合
/// </summary>
/// <param name="FieldConfigs"></param>
/// <param name="ValidationRules"></param>
/// <param name="DropdownConfigs"></param>
/// <param name="DropdownOptions"></param>
public sealed record FieldConfigData(
    List<FormFieldConfigDto> FieldConfigs,
    List<FormFieldValidationRuleDto> ValidationRules,
    List<FormFieldDropDownDto> DropdownConfigs,
    List<FormFieldDropdownOptionsDto> DropdownOptions);

