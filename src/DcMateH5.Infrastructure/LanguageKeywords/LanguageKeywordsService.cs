using System.Data;
using Dapper;
using DbExtensions.DbExecutor.Interface;
using DcMateH5.Abstractions.Language.Models;
using DcMateH5.Abstractions.LanguageKeywords;
using DcMateH5.Abstractions.Menu.Models;

namespace DcMateH5.Infrastructure.LanguageKeywords
{
    /// <summary>
    /// 語系關鍵字服務
    /// </summary>
    public sealed class LanguageKeywordService : ILanguageKeywordService
    {
        private readonly IDbExecutor _db;
        
        public LanguageKeywordService(IDbExecutor db)
        {
            _db = db;
        }

        /// <summary>
        /// 取得指定語系列表的關鍵字資料
        /// </summary>
        /// <param name="languages">語系列表</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>多語系關鍵字資料</returns>
        public async Task<IReadOnlyList<LanguageKeywordDto>> GetKeywordsAsync(
            IReadOnlyCollection<string> languages,
            CancellationToken cancellationToken)
        {
            List<string> normalizedLanguages = NormalizeLanguages(languages);

            if (normalizedLanguages.Count == 0)
            {
                throw new ArgumentException("Languages 不可為空。", nameof(languages));
            }

            const string sql = @"
SELECT
    k.SID AS Sid,
    k.TYPE AS Type,
    k.KEYWORDS AS Keywords,
    k.DEFAULT_VALUE AS DefaultValue,
    l.LANGUAGE AS Language,
    l.VALUE AS Value
FROM dbo.CFG_LANG_KEYWORDS k
LEFT JOIN dbo.CFG_LANG_DATA l
    ON l.KEYWORD_SID = k.SID
   AND l.LANGUAGE IN @Languages
ORDER BY k.SID;";

            IEnumerable<LanguageKeywordRawDto> rawRows = await _db
                .QueryAsync<LanguageKeywordRawDto>(sql, new { Languages = normalizedLanguages })
                .ConfigureAwait(false);
            
            List<LanguageKeywordDto> result = rawRows
                .GroupBy(row => new
                {
                    row.Sid,
                    row.Type,
                    row.Keywords,
                    row.DefaultValue
                })
                .Select(group =>
                {
                    LanguageKeywordRawDto first = group.First();

                    Dictionary<string, string> translations = group
                        .Where(row => !string.IsNullOrWhiteSpace(row.Language))
                        .GroupBy(row => row.Language!, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            keySelector: languageGroup => languageGroup.Key,
                            elementSelector: languageGroup =>
                            {
                                LanguageKeywordRawDto row = languageGroup.First();
                                return row.Value ?? string.Empty;
                            },
                            comparer: StringComparer.OrdinalIgnoreCase);

                    return new LanguageKeywordDto
                    {
                        Sid = first.Sid,
                        Type = first.Type,
                        Key = first.Keywords,
                        DefaultValue = first.DefaultValue,
                        Translations = translations
                    };
                })
                .ToList();

            return result;
        }

        /// <summary>
        /// 正規化語系列表
        /// </summary>
        /// <param name="languages">原始語系列表</param>
        /// <returns>正規化後語系列表</returns>
        private static List<string> NormalizeLanguages(IReadOnlyCollection<string> languages)
        {
            if (languages == null)
            {
                return new List<string>();
            }

            List<string> normalizedLanguages = languages
                .Where(language => !string.IsNullOrWhiteSpace(language))
                .Select(language => language.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return normalizedLanguages;
        }
    }
}