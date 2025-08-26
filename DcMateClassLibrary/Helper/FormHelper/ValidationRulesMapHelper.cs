namespace ClassLibrary;

public static class ValidationRulesMap
{
    private static readonly Dictionary<FormControlType, ValidationType[]> _map = new()
    {
        { FormControlType.Text,     new[] { ValidationType.Regex } },
        { FormControlType.Number,   new[] { ValidationType.Min, ValidationType.Max } },
        { FormControlType.Date,     new[] { ValidationType.Min, ValidationType.Max } },
        { FormControlType.Checkbox, Array.Empty<ValidationType>() },
        { FormControlType.Textarea, new[] { ValidationType.Regex } },
        { FormControlType.Dropdown, Array.Empty<ValidationType>() },
    };

    public static ValidationType[] GetValidations(FormControlType controlType)
    {
        return _map.TryGetValue(controlType, out var types) ? types : Array.Empty<ValidationType>();
    }

    /// <summary>
    /// 檢查 元件 有沒有對應 限制條件
    /// </summary>
    /// <param name="controlType">元件類型</param>
    /// <returns></returns>
    public static bool HasValidations(FormControlType controlType)
    {
        var res = _map.TryGetValue(controlType, out var types) && types.Length > 0;
        return res;
    }
}
