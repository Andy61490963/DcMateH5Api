using System.Text.RegularExpressions;
using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.DbExtensions;

namespace DcMateH5Api.Areas.Form.Services;

public class FormDeleteGuardService : IFormDeleteGuardService
{
    private static readonly Regex SqlParameterRegex = new("@\\w+", RegexOptions.Compiled);
    private static readonly Regex ForbiddenKeywordRegex = new(
        @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|EXEC|WAITFOR)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IDbExecutor _db;

    public FormDeleteGuardService(IDbExecutor db)
    {
        _db = db;
    }

    /// <summary>
    /// 依序驗證刪除守門規則，當遇到不可刪除時立即回傳阻擋原因。
    /// </summary>
    /// <param name="request">刪除驗證請求內容</param>
    /// <param name="ct">取消權杖</param>
    /// <returns>Guard SQL 驗證結果</returns>
    public async Task<DeleteGuardValidateResultViewModel> ValidateDeleteGuardAsync(
        DeleteGuardValidateRequestViewModel request,
        CancellationToken ct = default)
    {
        var rules = await GetGuardRulesAsync(request.FormFieldMasterId, ct);
        foreach (var rule in rules)
        {
            var validation = ValidateGuardSql(rule, request.Key);
            if (!validation.IsValid)
            {
                return validation;
            }

            var parameters = BuildParameters(rule, request.Key, request.Value);
            var canDelete = await ExecuteGuardSqlAsync(rule, parameters, ct);
            if (!canDelete.HasValue)
            {
                return BuildInvalidResult("Guard SQL 未回傳 CanDelete 欄位或結果為空。");
            }

            if (!canDelete.Value)
            {
                return new DeleteGuardValidateResultViewModel
                {
                    IsValid = true,
                    CanDelete = false,
                    BlockedByRule = rule.NAME
                };
            }
        }

        return new DeleteGuardValidateResultViewModel
        {
            IsValid = true,
            CanDelete = true,
            BlockedByRule = null
        };
    }

    /// <summary>
    /// 取得指定表單的刪除守門規則清單，並依規則順序排序。
    /// </summary>
    private async Task<List<FormFieldDeleteGuardSqlDto>> GetGuardRulesAsync(
        Guid formFieldMasterId,
        CancellationToken ct)
    {
        const string sql = @"
SELECT
    ID,
    FORM_FIELD_MASTER_ID,
    NAME,
    GUARD_SQL,
    IS_ENABLED,
    RULE_ORDER,
    IS_DELETE
FROM FORM_FIELD_DELETE_GUARD_SQL
WHERE FORM_FIELD_MASTER_ID = @FormFieldMasterId
  AND IS_ENABLED = 1
  AND IS_DELETE = 0
ORDER BY RULE_ORDER";

        var rules = await _db.QueryAsync<FormFieldDeleteGuardSqlDto>(
            sql,
            new { FormFieldMasterId = formFieldMasterId },
            ct: ct);

        return rules
            .OrderBy(x => x.RULE_ORDER ?? int.MaxValue)
            .ToList();
    }

    /// <summary>
    /// 驗證 Guard SQL 格式與參數規則。
    /// </summary>
    private static DeleteGuardValidateResultViewModel ValidateGuardSql(
        FormFieldDeleteGuardSqlDto rule,
        string key)
    {
        if (string.IsNullOrWhiteSpace(rule.GUARD_SQL))
        {
            return BuildInvalidResult("Guard SQL 不可為空。");
        }

        var sql = rule.GUARD_SQL.Trim();
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return BuildInvalidResult("Guard SQL 必須以 SELECT 開頭。");
        }

        if (sql.Contains(';'))
        {
            return BuildInvalidResult("Guard SQL 不可包含分號。");
        }

        if (ForbiddenKeywordRegex.IsMatch(sql))
        {
            return BuildInvalidResult("Guard SQL 包含禁止的關鍵字。");
        }

        var parameters = ExtractParameters(sql);
        if (!parameters.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            return BuildInvalidResult("輸入的 Key 未包含在 Guard SQL 參數中。");
        }

        return new DeleteGuardValidateResultViewModel { IsValid = true };
    }

    /// <summary>
    /// 解析 Guard SQL 內的參數名稱，去除 @ 後回傳唯一清單。
    /// </summary>
    private static List<string> ExtractParameters(string sql)
    {
        return SqlParameterRegex.Matches(sql)
            .Select(match => match.Value.TrimStart('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 建立 Dapper 參數，避免使用字串替換注入風險。
    /// </summary>
    private static DynamicParameters BuildParameters(
        FormFieldDeleteGuardSqlDto rule,
        string key,
        string value)
    {
        var sql = rule.GUARD_SQL?.Trim() ?? string.Empty;
        var parameters = ExtractParameters(sql);
        var matchedKey = parameters.First(name =>
            string.Equals(name, key, StringComparison.OrdinalIgnoreCase));

        var dynamicParameters = new DynamicParameters();
        dynamicParameters.Add(matchedKey, value);
        return dynamicParameters;
    }

    /// <summary>
    /// 執行 Guard SQL 並取得 CanDelete 結果。
    /// </summary>
    private async Task<bool?> ExecuteGuardSqlAsync(
        FormFieldDeleteGuardSqlDto rule,
        DynamicParameters parameters,
        CancellationToken ct)
    {
        var sql = rule.GUARD_SQL ?? string.Empty;
        var result = await _db.QuerySingleOrDefaultAsync<GuardSqlResult>(
            sql,
            parameters,
            ct: ct);

        return result?.CanDelete;
    }

    /// <summary>
    /// 建立無效 Guard SQL 的回傳結果。
    /// </summary>
    private static DeleteGuardValidateResultViewModel BuildInvalidResult(string message)
    {
        return new DeleteGuardValidateResultViewModel
        {
            IsValid = false,
            ErrorMessage = message,
            CanDelete = false
        };
    }

    private class GuardSqlResult
    {
        public bool? CanDelete { get; set; }
    }
}
