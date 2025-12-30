using System.Text.RegularExpressions;
using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.DbExtensions;

namespace DcMateH5Api.Areas.Form.Services;

/// <summary>
/// 表單刪除守門規則驗證服務
/// </summary>
public sealed class FormDeleteGuardService : IFormDeleteGuardService
{
    /// <summary>
    /// 用來抓 SQL 參數（@EQP_NO、@SID...）
    /// </summary>
    private static readonly Regex SqlParameterRegex =
        new(@"@\w+", RegexOptions.Compiled);

    /// <summary>
    /// 禁止的 SQL 關鍵字（最低限度防護）
    /// </summary>
    private static readonly Regex ForbiddenKeywordRegex =
        new(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|EXEC|WAITFOR)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IDbExecutor _db;

    public FormDeleteGuardService(IDbExecutor db)
    {
        _db = db;
    }

    /// <summary>
    /// 依序驗證 Guard SQL，遇到第一條不可刪除即回傳
    /// </summary>
    public async Task<DeleteGuardValidateResultViewModel> ValidateDeleteGuardAsync(
        DeleteGuardValidateRequestViewModel request,
        CancellationToken ct = default)
    {
        // 1️⃣ 撈出所有 Guard 規則
        var rules = await GetGuardRulesAsync(request.FormFieldMasterId, ct);

        foreach (var rule in rules)
        {
            // 2️⃣ 驗證 SQL 基本安全性與參數完整性
            var validation = ValidateGuardSql(rule, request.Parameters);
            if (!validation.IsValid)
            {
                return validation;
            }

            // 3️⃣ 建立 Dapper 參數（只塞 SQL 用得到的）
            var parameters = BuildParameters(rule.GUARD_SQL!, request.Parameters);

            // 4️⃣ 執行 Guard SQL
            var canDelete = await ExecuteGuardSqlAsync(rule, parameters, ct);

            // SQL 沒回傳或回傳格式不對 → 視為規則錯誤
            if (!canDelete.HasValue)
            {
                return BuildInvalidResult("Guard SQL 未回傳 CanDelete 結果。");
            }

            // 只要一條規則不允許 → 直接擋
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

        // 全部通過
        return new DeleteGuardValidateResultViewModel
        {
            IsValid = true,
            CanDelete = true
        };
    }

    #region Private Methods

    /// <summary>
    /// 取得啟用中的 Guard 規則
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
            .OrderBy(r => r.RULE_ORDER ?? int.MaxValue)
            .ToList();
    }

    /// <summary>
    /// 驗證 Guard SQL 本身是否合法、參數是否齊全
    /// </summary>
    private static DeleteGuardValidateResultViewModel ValidateGuardSql(
        FormFieldDeleteGuardSqlDto rule,
        Dictionary<string, string> inputParameters)
    {
        if (string.IsNullOrWhiteSpace(rule.GUARD_SQL))
        {
            return BuildInvalidResult("Guard SQL 不可為空。");
        }

        var sql = rule.GUARD_SQL.Trim();

        // 1️⃣ 只能 SELECT 開頭
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return BuildInvalidResult("Guard SQL 必須以 SELECT 開頭。");
        }

        // 2️⃣ 禁止分號（避免多段 SQL）
        if (sql.Contains(';'))
        {
            return BuildInvalidResult("Guard SQL 不可包含分號。");
        }

        // 3️⃣ 禁止危險關鍵字
        if (ForbiddenKeywordRegex.IsMatch(sql))
        {
            return BuildInvalidResult("Guard SQL 包含禁止的關鍵字。");
        }

        // 4️⃣ 解析 SQL 需要的參數
        var requiredParams = ExtractParameters(sql);

        // SQL 需要的，每一個都必須由前端提供
        var missing = requiredParams
            .Where(p => !inputParameters.Keys
                .Any(k => string.Equals(k, p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Any())
        {
            return BuildInvalidResult(
                $"缺少 Guard SQL 參數：{string.Join(", ", missing)}");
        }

        return new DeleteGuardValidateResultViewModel { IsValid = true };
    }

    /// <summary>
    /// 從 SQL 中抽出所有參數名稱（不含 @）
    /// </summary>
    private static List<string> ExtractParameters(string sql)
    {
        return SqlParameterRegex.Matches(sql)
            .Select(m => m.Value.TrimStart('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 建立 Dapper DynamicParameters（僅放 SQL 用得到的參數）
    /// </summary>
    private static DynamicParameters BuildParameters(
        string sql,
        Dictionary<string, string> inputParameters)
    {
        var sqlParams = ExtractParameters(sql);
        var parameters = new DynamicParameters();

        foreach (var sqlParam in sqlParams)
        {
            var inputKey = inputParameters.Keys
                .First(k => string.Equals(k, sqlParam, StringComparison.OrdinalIgnoreCase));

            parameters.Add(sqlParam, inputParameters[inputKey]);
        }

        return parameters;
    }

    /// <summary>
    /// 執行 Guard SQL，取得 CanDelete
    /// </summary>
    private async Task<bool?> ExecuteGuardSqlAsync(
        FormFieldDeleteGuardSqlDto rule,
        DynamicParameters parameters,
        CancellationToken ct)
    {
        var result = await _db.QuerySingleOrDefaultAsync<GuardSqlResult>(
            rule.GUARD_SQL!,
            parameters,
            ct: ct);

        return result?.CanDelete;
    }

    /// <summary>
    /// 統一建立錯誤回傳
    /// </summary>
    private static DeleteGuardValidateResultViewModel BuildInvalidResult(string message)
    {
        return new DeleteGuardValidateResultViewModel
        {
            IsValid = false,
            CanDelete = false,
            ErrorMessage = message
        };
    }

    #endregion

    /// <summary>
    /// Guard SQL 查詢結果 Model
    /// </summary>
    private sealed class GuardSqlResult
    {
        public bool? CanDelete { get; set; }
    }
}
