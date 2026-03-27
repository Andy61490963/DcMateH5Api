namespace DcMateH5.Abstractions.Language.Models;

/// <summary>
/// 建立多語系關鍵字請求
/// </summary>
public sealed class CreateLanguageKeywordRequest
{
    /// <summary>
    /// 關鍵字類型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 關鍵字
    /// </summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>
    /// 預設值
    /// </summary>
    public string? DefaultValue { get; set; }
}

/// <summary>
/// 建立多語系關鍵字結果
/// </summary>
public sealed class CreateLanguageKeywordResponse
{
    /// <summary>
    /// CFG_LANG_KEYWORDS.SID
    /// </summary>
    public decimal KeywordSid { get; set; }

    /// <summary>
    /// 實際建立的語系資料筆數
    /// </summary>
    public int CreatedLanguageDataCount { get; set; }
}

/// <summary>
/// 語系代碼資料
/// </summary>
public sealed class LanguageCodeDto
{
    /// <summary>
    /// 語系代碼
    /// </summary>
    public string LangCode { get; set; } = string.Empty;
}