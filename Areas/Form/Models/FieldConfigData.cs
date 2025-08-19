namespace DynamicForm.Areas.Form.Models;

public sealed record FieldConfigData(
    List<FormFieldConfigDto> FieldConfigs,
    List<FormFieldValidationRuleDto> ValidationRules,
    List<FORM_FIELD_DROPDOWN> DropdownConfigs,
    List<FORM_FIELD_DROPDOWN_OPTIONS> DropdownOptions);

