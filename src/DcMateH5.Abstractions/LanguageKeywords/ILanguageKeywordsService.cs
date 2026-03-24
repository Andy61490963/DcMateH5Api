using DcMateH5.Abstractions.Language.Models;

namespace DcMateH5.Abstractions.LanguageKeywords;

/// <summary>
/// 語系關鍵字服務
/// </summary>
public interface ILanguageKeywordService
{
    /// <summary>
    /// 取得指定語系列表的關鍵字資料
    /// </summary>
    /// <param name="languages">語系列表</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>多語系關鍵字資料</returns>
    Task<IReadOnlyList<LanguageKeywordDto>> GetKeywordsAsync(
        IReadOnlyCollection<string> languages,
        CancellationToken cancellationToken);
}