namespace DynamicForm.Areas.Enum.Models;

public sealed class EnumOptionDto
{
    /// <summary>枚舉的整數值</summary>
    public int Value { get; init; }

    /// <summary>枚舉原生名稱（程式用）</summary>
    public string? Key { get; init; }

    /// <summary>顯示名稱（DisplayAttribute），無則回退為 Key</summary>
    public string? Text { get; init; }
    
    /// <summary> 額外描述 </summary>
    public string? Description { get; init; }
}