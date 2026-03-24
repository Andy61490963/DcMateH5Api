namespace DcMateH5.Abstractions.Language.Models;

/// <summary>
/// 語系關鍵字原始查詢結果
/// </summary>
public sealed class LanguageKeywordRawDto
{
    /// <summary>
    /// 關鍵字 SID
    /// </summary>
    public decimal Sid { get; set; }

    /// <summary>
    /// 類型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 關鍵字
    /// </summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>
    /// 預設值
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// 語系
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// 翻譯值
    /// </summary>
    public string? Value { get; set; }
}