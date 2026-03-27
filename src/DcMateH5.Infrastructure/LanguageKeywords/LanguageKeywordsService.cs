using ClassLibrary;
using ClassLibrary.Areas.LanguageKeywords;
using DbExtensions.DbExecutor.Interface;
using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Language.Models;
using DcMateH5.Abstractions.LanguageKeywords;
using DcMateH5Api.Models;
using Microsoft.Data.SqlClient;

namespace DcMateH5.Infrastructure.LanguageKeywords
{
    /// <summary>
    /// 語系關鍵字服務
    /// </summary>
    public sealed class LanguageKeywordService : ILanguageKeywordService
    {
        private static class SqlStatements
        {
            public const string GetLanguageCodes = @"
SELECT
    LANG_CODE AS LangCode
FROM dbo.CFG_LANG_TYPE
WHERE LANG_CODE IS NOT NULL
ORDER BY SEQ, SID;";

            public const string ExistsKeyword = @"
SELECT COUNT(1)
FROM dbo.CFG_LANG_KEYWORDS
WHERE TYPE = @Type
  AND KEYWORDS = @Keywords;";

            public const string InsertKeyword = @"
INSERT INTO dbo.CFG_LANG_KEYWORDS
(
    SID,
    TYPE,
    KEYWORDS,
    DEFAULT_VALUE,
    CREATE_USER,
    CREATE_TIME,
    EDIT_USER,
    EDIT_TIME
)
VALUES
(
    @Sid,
    @Type,
    @Keywords,
    @DefaultValue,
    @CreateUser,
    @CreateTime,
    @EditUser,
    @EditTime
);";

            public const string InsertLanguageData = @"
INSERT INTO dbo.CFG_LANG_DATA
(
    SID,
    KEYWORD_SID,
    LANGUAGE,
    VALUE,
    CREATE_USER,
    CREATE_TIME,
    EDIT_USER,
    EDIT_TIME
)
VALUES
(
    @Sid,
    @KeywordSid,
    @Language,
    @Value,
    @CreateUser,
    @CreateTime,
    @EditUser,
    @EditTime
);";
        }

        private static class DefaultValues
        {
            public const string SystemUser = "SYSTEM";
        }

        private readonly IDbExecutor _db;
        private readonly ICurrentUserAccessor _currentUser;

        public LanguageKeywordService(IDbExecutor db, ICurrentUserAccessor currentUser)
        {
            _db = db;
            _currentUser = currentUser; 
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
                throw new ArgumentException("Languages cannot be empty.", nameof(languages));
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
                .QueryAsync<LanguageKeywordRawDto>(sql, new { Languages = normalizedLanguages }, ct: cancellationToken)
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
        /// 建立多語系關鍵字
        /// </summary>
        /// <param name="request">建立請求</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>建立結果</returns>
        public async Task<Result<CreateLanguageKeywordResponse>>  CreateAsync(
            CreateLanguageKeywordRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                ValidateCreateRequest(request);

                string normalizedType = request.Type.Trim();
                string normalizedKeywords = request.Keywords.Trim();

                bool keywordExists = await KeywordExistsAsync(
                    normalizedType,
                    normalizedKeywords,
                    cancellationToken);

                if (keywordExists)
                {
                    return Result<CreateLanguageKeywordResponse>.Fail(
                        LanguageKeywordsErrorCode.KeyWordExisted,
                        $"Keyword '{normalizedKeywords}' already exists.");
                }

                List<string> languageCodes = await GetLanguageCodesAsync(cancellationToken);
                if (languageCodes.Count == 0)
                {
                    throw new InvalidOperationException("No language codes were found in CFG_LANG_TYPE.");
                }

                DateTime now = DateTime.Now;

                var userName = _currentUser.Get().Account;

                decimal keywordSid = RandomHelper.GenerateRandomDecimal();

                await _db.TxAsync(async (_, _, ct) =>
                {
                    await InsertKeywordAsync(
                        keywordSid,
                        request,
                        userName,
                        now,
                        ct);

                    IReadOnlyList<CreateLanguageDataRow> languageDataRows = BuildLanguageDataRows(
                        keywordSid,
                        languageCodes,
                        userName,
                        now);

                    await InsertLanguageDataAsync(languageDataRows, ct);
                }, ct: cancellationToken);

                return Result<CreateLanguageKeywordResponse>.Ok(
                    new CreateLanguageKeywordResponse
                    {
                        KeywordSid = keywordSid,
                        CreatedLanguageDataCount = languageCodes.Count
                    });
            }
            catch (SqlException ex) when (ex.Number == 2627)
            {
                throw new InvalidOperationException($"Keyword '{request.Keywords?.Trim()}' already exists.");
            }
        }

        /// <summary>
        /// 檢查關鍵字是否已存在
        /// </summary>
        /// <param name="type">關鍵字類型</param>
        /// <param name="keywords">關鍵字</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>是否存在</returns>
        private async Task<bool> KeywordExistsAsync(
            string type,
            string keywords,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<int> rows = await _db.QueryAsync<int>(
                SqlStatements.ExistsKeyword,
                new
                {
                    Type = type,
                    Keywords = keywords
                },
                ct: cancellationToken);

            return rows.FirstOrDefault() > 0;
        }
        
        /// <summary>
        /// 驗證建立請求
        /// </summary>
        /// <param name="request">建立請求</param>
        private static void ValidateCreateRequest(CreateLanguageKeywordRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Type))
            {
                throw new ArgumentException("Type cannot be empty.", nameof(request.Type));
            }

            if (string.IsNullOrWhiteSpace(request.Keywords))
            {
                throw new ArgumentException("Keywords cannot be empty.", nameof(request.Keywords));
            }
        }

        /// <summary>
        /// 取得所有語系代碼
        /// </summary>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>語系代碼清單</returns>
        private async Task<List<string>> GetLanguageCodesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<LanguageCodeDto> rows = await _db.QueryAsync<LanguageCodeDto>(
                SqlStatements.GetLanguageCodes,
                ct: cancellationToken);

            List<string> languageCodes = rows
                .Where(row => !string.IsNullOrWhiteSpace(row.LangCode))
                .Select(row => row.LangCode.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return languageCodes;
        }

        /// <summary>
        /// 新增關鍵字主檔
        /// </summary>
        /// <param name="keywordSid">關鍵字 SID</param>
        /// <param name="request">建立請求</param>
        /// <param name="userName">異動人員</param>
        /// <param name="now">異動時間</param>
        /// <param name="cancellationToken">取消權杖</param>
        private async Task InsertKeywordAsync(
            decimal keywordSid,
            CreateLanguageKeywordRequest request,
            string userName,
            DateTime now,
            CancellationToken cancellationToken)
        {
            CreateKeywordRow row = new CreateKeywordRow
            {
                Sid = keywordSid,
                Type = request.Type.Trim(),
                Keywords = request.Keywords.Trim(),
                DefaultValue = NormalizeNullableText(request.DefaultValue),
                CreateUser = userName,
                CreateTime = now,
                EditUser = userName,
                EditTime = now
            };

            await _db.ExecuteAsync(
                SqlStatements.InsertKeyword,
                row,
                ct: cancellationToken);
        }

        /// <summary>
        /// 建立語系資料列
        /// </summary>
        /// <param name="keywordSid">關鍵字 SID</param>
        /// <param name="languageCodes">語系代碼</param>
        /// <param name="userName">異動人員</param>
        /// <param name="now">異動時間</param>
        /// <returns>語系資料列</returns>
        private static IReadOnlyList<CreateLanguageDataRow> BuildLanguageDataRows(
            decimal keywordSid,
            IReadOnlyCollection<string> languageCodes,
            string userName,
            DateTime now)
        {
            List<CreateLanguageDataRow> rows = new List<CreateLanguageDataRow>();

            foreach (string languageCode in languageCodes)
            {
                rows.Add(new CreateLanguageDataRow
                {
                    Sid = RandomHelper.GenerateRandomDecimal(),
                    KeywordSid = keywordSid,
                    Language = languageCode,
                    Value = null,
                    CreateUser = userName,
                    CreateTime = now,
                    EditUser = userName,
                    EditTime = now
                });
            }

            return rows;
        }

        /// <summary>
        /// 批次新增語系資料
        /// </summary>
        /// <param name="rows">語系資料列</param>
        /// <param name="cancellationToken">取消權杖</param>
        private async Task InsertLanguageDataAsync(
            IReadOnlyList<CreateLanguageDataRow> rows,
            CancellationToken cancellationToken)
        {
            if (rows.Count == 0)
            {
                return;
            }

            await _db.ExecuteAsync(
                SqlStatements.InsertLanguageData,
                rows,
                ct: cancellationToken);
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

        /// <summary>
        /// 正規化可為空文字
        /// </summary>
        /// <param name="value">原始文字</param>
        /// <returns>正規化結果</returns>
        private static string? NormalizeNullableText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }
        
        /// <summary>
        /// 新增關鍵字資料列
        /// </summary>
        private sealed class CreateKeywordRow
        {
            public decimal Sid { get; set; }

            public string Type { get; set; } = string.Empty;

            public string Keywords { get; set; } = string.Empty;

            public string? DefaultValue { get; set; }

            public string CreateUser { get; set; } = string.Empty;

            public DateTime CreateTime { get; set; }

            public string EditUser { get; set; } = string.Empty;

            public DateTime EditTime { get; set; }
        }

        /// <summary>
        /// 新增語系資料列
        /// </summary>
        private sealed class CreateLanguageDataRow
        {
            public decimal Sid { get; set; }

            public decimal KeywordSid { get; set; }

            public string Language { get; set; } = string.Empty;

            public string? Value { get; set; }

            public string CreateUser { get; set; } = string.Empty;

            public DateTime CreateTime { get; set; }

            public string EditUser { get; set; } = string.Empty;

            public DateTime EditTime { get; set; }
        }
    }
}