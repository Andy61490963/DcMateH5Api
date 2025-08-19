using ClassLibrary;
using DynamicForm.Areas.Form.Models;

namespace DynamicForm.Areas.Form.ViewModels;

public class FormFieldInputViewModel
{
    public Guid FieldConfigId { get; set; }
    public string Column { get; set; } = string.Empty;
    public string DATA_TYPE { get; set; }
    public FormControlType CONTROL_TYPE { get; set; }
    public string? DefaultValue { get; set; }
    public bool IS_REQUIRED { get; set; }
    public bool IS_EDITABLE { get; set; }

    public List<FormFieldValidationRuleDto>? ValidationRules { get; set; }

    public bool ISUSESQL { get; set; }
    public string DROPDOWNSQL { get; set; } = string.Empty;
    
    public QueryConditionType QUERY_CONDITION_TYPE { get; set; }
    public bool CAN_QUERY { get; set; }
    public List<FORM_FIELD_DROPDOWN_OPTIONS> OptionList { get; set; } = new();

    /// <summary>
    /// 若欄位來自 View，可紀錄其實際來源表
    /// </summary>
    public TableSchemaQueryType? SOURCE_TABLE { get; set; }

    public object? CurrentValue { get; set; }
}

