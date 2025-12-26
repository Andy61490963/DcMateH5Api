using ClassLibrary;
using DcMateH5Api.Areas.Form.Models;

namespace DcMateH5Api.Areas.Form.ViewModels;

public class FormFieldInputViewModel
{
    public Guid FieldConfigId { get; set; }
    public string Column { get; set; } = string.Empty;
    public string DATA_TYPE { get; set; }
    public FormControlType CONTROL_TYPE { get; set; }
    public string? DefaultValue { get; set; }
    public bool IS_REQUIRED { get; set; }
    public bool IS_EDITABLE { get; set; }
    public bool IS_PK { get; set; }
    public bool IS_RELATION { get; set; }

    public List<FormFieldValidationRuleDto>? ValidationRules { get; set; }

    public bool ISUSESQL { get; set; }
    public string DROPDOWNSQL { get; set; } = string.Empty;
    
    public QueryComponentType QUERY_COMPONENT { get; set; }
    
    public ConditionType QUERY_CONDITION { get; set; }
    
    public bool CAN_QUERY { get; set; }
    public List<FormFieldDropdownOptionsDto> OptionList { get; set; } = new();

    /// <summary>
    /// 由匯入 SQL 查詢得到的歷史查詢下拉清單（來源欄位需命名為 NAME）。
    /// </summary>
    public List<string> PREVIOUS_QUERY_LIST { get; set; } = new();

    /// <summary>
    /// 若欄位來自 View，可紀錄其實際來源表
    /// </summary>
    public TableSchemaQueryType? SOURCE_TABLE { get; set; }

    public object? CurrentValue { get; set; }
}
