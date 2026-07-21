using DcMateClassLibrary.Enums.Form;

namespace DcMateH5.Abstractions.Form.ViewModels;

/// <summary>
/// 單一 Mapping Row 的動態元件選項。
/// </summary>
public sealed class MappingComponentOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int Order { get; set; }
}

/// <summary>
/// Designer 儲存單一 Mapping Row 元件設定的請求。
/// </summary>
public sealed class MappingComponentUpsertViewModel
{
    public FormControlType ControlType { get; set; }
    public bool IsUseSql { get; set; }
    public string? DropdownSql { get; set; }
    public List<MappingComponentOptionViewModel> Options { get; set; } = new();
}

/// <summary>
/// Runtime 更新單一 Mapping Row 元件值的請求。
/// </summary>
public sealed class MappingComponentValueUpdateViewModel
{
    public object? Value { get; set; }
}

/// <summary>
/// Runtime 呈現單一 Mapping Row 所需的元件資料。
/// </summary>
public sealed class MultipleMappingComponentViewModel
{
    public string MappingRowId { get; set; } = string.Empty;
    public string DetailPk { get; set; } = string.Empty;
    public FormControlType ControlType { get; set; } = FormControlType.None;
    public object? CurrentValue { get; set; }
    public List<MappingComponentOptionViewModel> Options { get; set; } = new();
    public bool IsConfigured { get; set; }
}

/// <summary>
/// Designer 查詢單一 Mapping Row 元件設定的結果。
/// </summary>
public sealed class MappingComponentDesignerItemViewModel
{
    public string MappingRowId { get; set; } = string.Empty;
    public string DetailPk { get; set; } = string.Empty;
    public FormControlType ControlType { get; set; } = FormControlType.None;
    public object? CurrentValue { get; set; }
    public bool IsUseSql { get; set; }
    public string? DropdownSql { get; set; }
    public List<MappingComponentOptionViewModel> Options { get; set; } = new();
    public bool IsConfigured { get; set; }
}

/// <summary>
/// Designer 逐 SID 元件查詢結果。
/// </summary>
public sealed class MappingComponentDesignerListViewModel
{
    public Guid FormMasterId { get; set; }
    public string? MappingComponentTargetColumnName { get; set; }
    public int TotalCount { get; set; }
    public Dictionary<string, MappingComponentDesignerItemViewModel> ComponentsByMappingRowId { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}
