namespace DcMateH5.Abstractions.Language.Models;

/// <summary>
/// 多語系關鍵字查詢輸入模型
/// </summary>
public sealed class LanguageKeywordsRequest
{
    /// <summary>
    /// 語系列表
    /// </summary>
    public List<string> Languages { get; set; } = new List<string>();
}