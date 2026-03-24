namespace DcMateH5.Abstractions.Language.Models;

/// <summary>
/// 多語系關鍵字查詢結果
/// </summary>
public sealed class LanguageKeywordDto
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
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 預設值
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// 各語系翻譯
    /// </summary>
    public Dictionary<string, string> Translations { get; set; } = new Dictionary<string, string>();
}