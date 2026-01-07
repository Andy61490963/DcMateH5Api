using System.Text.RegularExpressions;
using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.DbExtensions;
using Microsoft.Data.SqlClient;

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

    public async Task<DeleteGuardValidateResultViewModel> ValidateDeleteGuardInternalAsync(
        Guid formFieldMasterId,
        Dictionary<string, string> parameters,
        SqlTransaction tx,
        CancellationToken ct)
    {
        var rules = await GetGuardRulesAsync(formFieldMasterId, tx, ct);

        if (rules.Count == 0)
        {
            return new DeleteGuardValidateResultViewModel
            {
                IsValid = false,
                CanDelete = false,
                ErrorMessage = "未設定刪除守門規則（Guard Rules），不允許刪除。"
            };
        }
        
        foreach (var rule in rules)
        {
            var validation = ValidateGuardSql(rule, parameters);
            if (!validation.IsValid)
                return validation;

            var dapperParams = BuildParameters(rule.GUARD_SQL!, parameters);

            var canDelete = await ExecuteGuardSqlAsync(rule, dapperParams, tx, ct);

            if (!canDelete.HasValue)
                return BuildInvalidResult("Guard SQL 未回傳 CanDelete 結果。");

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
        
        return new DeleteGuardValidateResultViewModel { IsValid = true, CanDelete = true };
    }

    #region Private Methods

    /// <summary>
    /// 取得啟用中的 Guard 規則
    /// </summary>
    private async Task<List<FormFieldDeleteGuardSqlDto>> GetGuardRulesAsync(
        Guid formFieldMasterId,
        SqlTransaction tx,
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

        var rules = await _db.QueryInTxAsync<FormFieldDeleteGuardSqlDto>(
            tx.Connection,
            tx,
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

        // 只能 SELECT 開頭
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return BuildInvalidResult("Guard SQL 必須以 SELECT 開頭。");
        }

        // 禁止分號（避免多段 SQL）
        if (sql.Contains(';'))
        {
            return BuildInvalidResult("Guard SQL 不可包含分號。");
        }

        // 禁止危險關鍵字
        if (ForbiddenKeywordRegex.IsMatch(sql))
        {
            return BuildInvalidResult("Guard SQL 包含禁止的關鍵字。");
        }

        // 解析 SQL 需要的參數
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
    /// 執行 Guard SQL，取得 CanDelete（支援交易）
    /// </summary>
    private async Task<bool?> ExecuteGuardSqlAsync(
        FormFieldDeleteGuardSqlDto rule,
        DynamicParameters parameters,
        SqlTransaction tx,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rule.GUARD_SQL))
            return null;
        
        var result = await _db.QuerySingleOrDefaultInTxAsync<GuardSqlResult>(
            tx.Connection,
            tx,
            rule.GUARD_SQL!,
            param: parameters,
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
