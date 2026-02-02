using System.Data;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ClassLibrary;
using Dapper;
using DcMateH5Api.Helper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Services;

public class FormTableValueFunctionService : IFormTableValueFunctionService
{
    private const string DefaultSchemaName = "dbo";

    private static readonly Regex SafeSqlIdentifierRegex
        = new("^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly SqlConnection _con;
    private readonly ITransactionService _txService;
    private readonly IFormFieldMasterService _formFieldMasterService;
    private readonly ISchemaService _schemaService;
    private readonly IConfiguration _configuration;
    private readonly List<string> _excludeColumns;

    public FormTableValueFunctionService(
        SqlConnection connection,
        ITransactionService txService,
        IFormFieldMasterService formFieldMasterService,
        ISchemaService schemaService,
        IConfiguration configuration)
    {
        _con = connection;
        _txService = txService;
        _formFieldMasterService = formFieldMasterService;
        _schemaService = schemaService;
        _configuration = configuration;

        _excludeColumns = _configuration.GetSection(ConfigKeys.FormDesignerRequiredColumns).Get<List<string>>() ?? new();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableValueFunctionConfigViewModel>> GetFormMasters(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        const string sql = @"
SELECT 
    M.ID             AS Id,
    M.FORM_NAME      AS FormName,
    M.TVF_TABLE_ID   AS TableFunctionValueId,
    M.TVF_TABLE_NAME AS TableFunctionValueName
FROM FORM_FIELD_MASTER M
WHERE M.FUNCTION_TYPE = @funcType
  AND M.IS_DELETE = 0;

SELECT
    C.FORM_FIELD_MASTER_ID AS MasterId,
    C.COLUMN_NAME          AS ParameterName
FROM FORM_FIELD_CONFIG C
INNER JOIN FORM_FIELD_MASTER M
    ON M.ID = C.FORM_FIELD_MASTER_ID
WHERE M.SCHEMA_TYPE = @schemaType
  AND M.IS_DELETE = 0
  AND C.IS_TVF_QUERY_PARAMETER = 1
ORDER BY C.FORM_FIELD_MASTER_ID, C.FIELD_ORDER;";

        var args = new
        {
            funcType = FormFunctionType.TableValueFunctionMaintenance.ToInt(),
            schemaType = TableSchemaQueryType.OnlyTvf.ToInt()
        };

        using var grid = await _con.QueryMultipleAsync(
            new CommandDefinition(sql, args, cancellationToken: ct));

        var masters = (await grid.ReadAsync<TableValueFunctionConfigViewModel>()).ToList();
        var paramRows = (await grid.ReadAsync<TvfParamRow>()).ToList();

        var paramMap = paramRows
            .GroupBy(x => x.MasterId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ParameterName)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList());

        foreach (var m in masters)
        {
            // 行為保持原樣（即使 key 不一致）
            m.Parameter = paramMap.TryGetValue(m.TableFunctionValueId, out var list)
                ? list
                : new List<string>();
        }

        return masters;
    }

    /// <summary>
    /// 取得 TVF 表單列表頁所需的資料清單（含各欄位實際值），
    /// 並將 Dropdown 欄位的選項值（OptionId）轉換為顯示文字（OptionText）。
    /// </summary>
    /// <param name="funcType">表單功能類型</param>
    /// <param name="request">查詢條件與分頁資訊（可選）</param>
    public async Task<List<FormTvfListDataViewModel>> GetTvfFormList(
        FormFunctionType funcType,
        FormTvfSearchRequest? request = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var page = request?.Page ?? 0;
        var pageSize = request?.PageSize ?? 20;

        var conditions = request?.Conditions ?? new();
        var orderBys = request?.OrderBys;
        var tvfParamKvsFromRequest = request?.TvfParameters;
        var requestMasterId = request?.FormMasterId ?? Guid.Empty;

        var metas = await _formFieldMasterService
            .GetFormMetaAggregatesAsync(funcType, TableSchemaQueryType.All);

        if (requestMasterId != Guid.Empty)
        {
            metas = metas.Where(x => x.Master.ID == requestMasterId).ToList();
        }

        var results = new List<FormTvfListDataViewModel>();

        foreach (var (master, _) in metas)
        {
            ct.ThrowIfCancellationRequested();

            var tvfName = master.TVF_TABLE_NAME;
            if (string.IsNullOrWhiteSpace(tvfName) ||
                !SafeSqlIdentifierRegex.IsMatch(tvfName))
            {
                continue;
            }

            var fieldConfigs = await LoadFieldConfigsAsync(master.TVF_TABLE_ID, ct);
            var tvfParamKvs = ResolveTvfParametersOrThrow(
                master.ID,
                fieldConfigs,
                tvfParamKvsFromRequest);

            var rows = await _txService.WithTransactionAsync(
                async (conn, tx, ct) =>
                {
                    var schema = await _schemaService.GetObjectSchemaInTxAsync(
                        conn, tx, DefaultSchemaName, tvfName, ct);

                    if (schema.Count == 0)
                    {
                        return new List<IDictionary<string, object?>>();
                    }

                    var tvfParams = schema
                        .Where(x => x.isTvfQueryParameter)
                        .OrderBy(x => x.ORDINAL_POSITION)
                        .ToList();

                    var returnColumns = schema
                        .Where(x => !x.isTvfQueryParameter)
                        .OrderBy(x => x.ORDINAL_POSITION)
                        .ToList();

                    if (returnColumns.Count == 0)
                    {
                        return new List<IDictionary<string, object?>>();
                    }

                    var query = BuildTvfQuery(
                        DefaultSchemaName,
                        tvfName,
                        tvfParams,
                        returnColumns,
                        tvfParamKvs,
                        conditions,
                        orderBys,
                        page,
                        pageSize);

                    return await ExecuteRowsInTxAsync(conn, tx, query.Sql, query.Parameters, ct);
                },
                ct);

            if (rows.Count == 0)
            {
                continue;
            }

            var templates = BuildFieldTemplates(
                fieldConfigs,
                GetReturnColumnsFromConfigs(fieldConfigs));

            var dropdownMaps = BuildDropdownOptionMaps(templates);

            foreach (var row in rows)
            {
                var fields = templates.Select(t =>
                {
                    row.TryGetValue(t.Column, out var value);

                    if (t.IsDropdown &&
                        dropdownMaps.TryGetValue(t.FieldConfigId, out var map))
                    {
                        var key = value?.ToString();
                        if (!string.IsNullOrWhiteSpace(key) &&
                            map.TryGetValue(key, out var text))
                        {
                            value = text;
                        }
                    }

                    return new FormFieldInputViewModel
                    {
                        FieldConfigId = t.FieldConfigId,
                        Column = t.Column,
                        DISPLAY_NAME = t.DisplayName,
                        DATA_TYPE = t.DataType,
                        CONTROL_TYPE = t.ControlType,
                        CAN_QUERY = t.CanQuery,
                        QUERY_COMPONENT = t.QueryComponent,
                        QUERY_CONDITION = t.QueryCondition,
                        DefaultValue = t.DefaultValue,
                        IS_REQUIRED = t.IsRequired,
                        IS_EDITABLE = t.IsEditable,
                        IS_DISPLAYED = t.IsDisplayed,
                        ValidationRules = t.ValidationRules,
                        ISUSESQL = t.IsUseSql,
                        DROPDOWNSQL = t.DropdownSql,
                        OptionList = t.OptionList,
                        SOURCE_TABLE = t.SourceTable,
                        CurrentValue = value
                    };
                }).ToList();

                results.Add(new FormTvfListDataViewModel
                {
                    FormMasterId = master.ID,
                    FormName = master.FORM_NAME,
                    Fields = fields
                });
            }
        }

        return results;
    }


    // ============================================================
    // CHANGED: async transaction helper (avoid Task.FromResult + sync-over-async)
    // ============================================================

    private async Task<T> WithSqlTransactionAsync<T>(Func<SqlTransaction, Task<T>> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // NOTE: 這裡不改 DB schema、不加套件；以 SqlTransaction 直接控 Commit/Rollback
        //       交易隔離等策略若你專案有固定規範，可在這裡調整（目前保持預設行為）
        var needOpen = _con.State != ConnectionState.Open;
        if (needOpen)
        {
            await _con.OpenAsync(ct);
        }

        using var tx = _con.BeginTransaction();

        try
        {
            var result = await action(tx);
            tx.Commit();
            return result;
        }
        catch
        {
            try
            {
                tx.Rollback();
            }
            catch
            {
                // 保持原本例外優先，不吃掉主例外
            }

            throw;
        }
    }

    // ============================================================
    // CHANGED: TvfParameters resolver
    // ============================================================

    /// <summary>
    /// TvfParameters 為 null 時，自動從 FORM_FIELD_CONFIG 取 TVF_CURRENT_VALUE 補上；
    /// 若沒有任何 IS_TVF_QUERY_PARAMETER=1 則丟 400（依你的規則）。
    /// </summary>
    private static Dictionary<string, object?> ResolveTvfParametersOrThrow(
        Guid formMasterId,
        List<FormFieldConfigDto> fieldConfigs,
        Dictionary<string, object?>? tvfParamKvsFromRequest)
    {
        if (tvfParamKvsFromRequest != null)
        {
            return tvfParamKvsFromRequest;
        }

        var paramConfigs = fieldConfigs
            .Where(x => x.IS_TVF_QUERY_PARAMETER)
            .OrderBy(x => x.FIELD_ORDER)
            .ToList();

        if (paramConfigs.Count == 0)
        {
            throw new HttpStatusCodeException(
                HttpStatusCode.BadRequest,
                $"TVF 查詢參數未設定：FormMasterId={formMasterId} 對應的 TVF 表單沒有任何 IS_TVF_QUERY_PARAMETER = 1 的欄位。");
        }

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in paramConfigs)
        {
            var key = NormalizeTvfParamName(c.COLUMN_NAME);

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new HttpStatusCodeException(
                    HttpStatusCode.BadRequest,
                    $"TVF 查詢參數設定錯誤：FormMasterId={formMasterId} 的 COLUMN_NAME 不可為空。");
            }

            if (dict.ContainsKey(key))
            {
                throw new HttpStatusCodeException(
                    HttpStatusCode.BadRequest,
                    $"TVF 查詢參數設定錯誤：FormMasterId={formMasterId} 發現重複參數 COLUMN_NAME='{c.COLUMN_NAME}'（正規化後 key='{key}'）。");
            }

            dict[key] = c.TVF_CURRENT_VALUE;
        }

        return dict;
    }

    // ============================================================
    // configs (async)
    // ============================================================

    private async Task<List<FormFieldConfigDto>> LoadFieldConfigsAsync(
        Guid? masterId,
        CancellationToken ct)
    {
        const string sql = @"
SELECT *
FROM FORM_FIELD_CONFIG
WHERE FORM_FIELD_MASTER_ID = @MasterId
ORDER BY FIELD_ORDER;";

        var rows = await _con.QueryAsync<FormFieldConfigDto>(
            new CommandDefinition(sql, new { MasterId = masterId }, cancellationToken: ct));

        return rows.ToList();
    }

    private static IReadOnlyList<DbColumnInfo> GetReturnColumnsFromConfigs(List<FormFieldConfigDto> fieldConfigs)
    {
        return fieldConfigs
            .Where(x => !x.IS_TVF_QUERY_PARAMETER)
            .Select((x, idx) => new DbColumnInfo
            {
                COLUMN_NAME = x.COLUMN_NAME,
                DATA_TYPE = x.DATA_TYPE,
                ORDINAL_POSITION = idx + 1,
                IS_NULLABLE = x.COLUMN_IS_NULLABLE ? "YES" : "NO",
                isTvfQueryParameter = false
            })
            .ToList();
    }

    // ============================================================
    // SQL builder（TVF + WHERE + ORDER BY + Paging）
    // ============================================================

    private static TvfQuery BuildTvfQuery(
        string schemaName,
        string tvfName,
        IReadOnlyList<DbColumnInfo> tvfParams,
        IReadOnlyList<DbColumnInfo> returnColumns,
        Dictionary<string, object?>? tvfParamKvs,
        IReadOnlyList<FormTvfQueryConditionViewModel> conditions,
        IReadOnlyList<FormOrderBy>? orderBys,
        int page,
        int pageSize)
    {
        var dp = new DynamicParameters();

        BindTvfParameters(dp, tvfParams, tvfParamKvs);

        var whereSql = BuildReturnWhere(dp, returnColumns, conditions);

        var args = string.Join(", ", Enumerable.Range(0, tvfParams.Count).Select(i => $"@{SqlParamNames.TvfPrefix}{i}"));
        var fromSql = $"[{schemaName}].[{tvfName}]({args})";

        var sql = new StringBuilder();
        sql.Append("SELECT * FROM ");
        sql.Append(fromSql);
        sql.Append(whereSql);

        AppendOrderBy(sql, returnColumns, orderBys, page, pageSize);

        AppendPaging(sql, dp, page, pageSize);

        sql.Append(";");

        return new TvfQuery(sql.ToString(), dp);
    }

    private static void BindTvfParameters(
        DynamicParameters dp,
        IReadOnlyList<DbColumnInfo> tvfParams,
        Dictionary<string, object?>? tvfParamKvs)
    {
        var map = tvfParamKvs ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < tvfParams.Count; i++)
        {
            var p = tvfParams[i];
            var key = NormalizeTvfParamName(p.COLUMN_NAME);

            object? raw = null;
            if (map.TryGetValue(key, out var v))
            {
                raw = v;
            }

            var converted = ConvertToColumnTypeHelper.Convert(p.DATA_TYPE, raw);
            dp.Add($"@{SqlParamNames.TvfPrefix}{i}", converted);
        }
    }

    private static string BuildReturnWhere(
        DynamicParameters dp,
        IReadOnlyList<DbColumnInfo> returnColumns,
        IReadOnlyList<FormTvfQueryConditionViewModel> conditions)
    {
        if (conditions.Count == 0)
        {
            return string.Empty;
        }

        var typeMap = returnColumns
            .GroupBy(x => x.COLUMN_NAME, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DATA_TYPE, StringComparer.OrdinalIgnoreCase);

        var whereList = new List<string>();
        var i = 0;

        foreach (var c in conditions)
        {
            if (string.IsNullOrWhiteSpace(c.Column))
            {
                continue;
            }

            if (!SafeSqlIdentifierRegex.IsMatch(c.Column))
            {
                continue;
            }

            if (c.ConditionType == null)
            {
                continue;
            }

            if (!typeMap.TryGetValue(c.Column, out var dataType))
            {
                continue;
            }

            var colSql = $"[{c.Column}]";
            var p1 = $"@{SqlParamNames.WherePrefix}{i++}";

            switch (c.ConditionType.Value)
            {
                case ConditionType.Equal:
                    whereList.Add($"{colSql} = {p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, c.Value));
                    break;

                case ConditionType.NotEqual:
                    whereList.Add($"{colSql} <> {p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, c.Value));
                    break;

                case ConditionType.Like:
                    whereList.Add($"{colSql} LIKE {p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert("nvarchar", ToLikeValue(c.Value)));
                    break;

                case ConditionType.Between:
                    var p2 = $"@{SqlParamNames.WherePrefix}{i++}";
                    whereList.Add($"{colSql} BETWEEN {p1} AND {p2}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, c.Value));
                    dp.Add(p2, ConvertToColumnTypeHelper.Convert(dataType, c.Value2));
                    break;

                case ConditionType.GreaterThan:
                    whereList.Add($"{colSql} > {p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, c.Value));
                    break;

                case ConditionType.GreaterThanOrEqual:
                    whereList.Add($"{colSql} >= {p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, c.Value));
                    break;

                case ConditionType.LessThan:
                    whereList.Add($"{colSql} < {p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, c.Value));
                    break;

                case ConditionType.LessThanOrEqual:
                    whereList.Add($"{colSql} <= {p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, c.Value));
                    break;

                case ConditionType.In:
                    AppendIn(dp, whereList, colSql, dataType, c.Values, isNotIn: false, ref i);
                    break;

                case ConditionType.NotIn:
                    AppendIn(dp, whereList, colSql, dataType, c.Values, isNotIn: true, ref i);
                    break;
            }
        }

        if (whereList.Count == 0)
        {
            return string.Empty;
        }

        return " WHERE " + string.Join(" AND ", whereList);
    }

    private static void AppendOrderBy(
        StringBuilder sql,
        IReadOnlyList<DbColumnInfo> returnColumns,
        IReadOnlyList<FormOrderBy>? orderBys,
        int page,
        int pageSize)
    {
        var needOrderBy = page > 0 && pageSize > 0;

        if (!needOrderBy)
        {
            if (orderBys == null)
            {
                return;
            }
        }

        var allow = new HashSet<string>(returnColumns.Select(x => x.COLUMN_NAME), StringComparer.OrdinalIgnoreCase);
        var clauses = new List<string>();

        if (orderBys != null)
        {
            foreach (var ob in orderBys)
            {
                if (string.IsNullOrWhiteSpace(ob.Column))
                {
                    continue;
                }

                if (!SafeSqlIdentifierRegex.IsMatch(ob.Column))
                {
                    continue;
                }

                if (!allow.Contains(ob.Column))
                {
                    continue;
                }

                var dir = ob.Direction == SortType.Desc ? "DESC" : "ASC";
                clauses.Add($"[{ob.Column}] {dir}");
            }
        }

        if (clauses.Count == 0)
        {
            sql.Append(" ORDER BY (SELECT NULL)");
            return;
        }

        sql.Append(" ORDER BY ");
        sql.Append(string.Join(", ", clauses));
    }

    private static void AppendPaging(
        StringBuilder sql,
        DynamicParameters param,
        int page,
        int pageSize)
    {
        if (page <= 0 || pageSize <= 0)
        {
            return;
        }

        var p = Math.Max(page, 1);
        var ps = Math.Max(pageSize, 1);

        sql.Append(" OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY");
        param.Add("offset", (p - 1) * ps);
        param.Add("pageSize", ps);
    }

    private static void AppendIn(
        DynamicParameters dp,
        List<string> whereList,
        string colSql,
        string dataType,
        List<object>? values,
        bool isNotIn,
        ref int i)
    {
        if (values == null || values.Count == 0)
        {
            return;
        }

        var inParams = new List<string>(values.Count);

        foreach (var v in values)
        {
            var p = $"@{SqlParamNames.WherePrefix}{i++}";
            inParams.Add(p);
            dp.Add(p, ConvertToColumnTypeHelper.Convert(dataType, v));
        }

        var op = isNotIn ? "NOT IN" : "IN";
        whereList.Add($"{colSql} {op} ({string.Join(", ", inParams)})");
    }

    private static string? ToLikeValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return $"%{raw}%";
    }

    private static string NormalizeTvfParamName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return name[0] == '@' ? name.Substring(1) : name;
    }

    // ============================================================
    // Execute（tx 內，reader -> Dictionary） (async)
    // ============================================================

    private static async Task<List<IDictionary<string, object?>>> ExecuteRowsInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        string sql,
        DynamicParameters param,
        CancellationToken ct)
    {
        using var reader = await conn.ExecuteReaderAsync(
            new CommandDefinition(sql, param, transaction: tx, cancellationToken: ct));

        var rows = new List<IDictionary<string, object?>>();

        while (await reader.ReadAsync(ct))
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var isNull = await reader.IsDBNullAsync(i, ct);
                dict[reader.GetName(i)] = isNull ? null : reader.GetValue(i);
            }

            rows.Add(dict);
        }

        return rows;
    }

    // ============================================================
    // Templates / Dropdown map
    // ============================================================

    private List<FieldTemplate> BuildFieldTemplates(
        List<FormFieldConfigDto> fieldConfigs,
        IReadOnlyList<DbColumnInfo> returnColumns)
    {
        var returnSet = new HashSet<string>(returnColumns.Select(x => x.COLUMN_NAME), StringComparer.OrdinalIgnoreCase);

        return fieldConfigs
            .Where(c => !string.IsNullOrWhiteSpace(c.COLUMN_NAME))
            .Where(c => returnSet.Contains(c.COLUMN_NAME))
            .Where(c => !_excludeColumns.Contains(c.COLUMN_NAME, StringComparer.OrdinalIgnoreCase))
            .Select(c =>
            {
                var col = returnColumns.First(x => string.Equals(x.COLUMN_NAME, c.COLUMN_NAME, StringComparison.OrdinalIgnoreCase));

                return new FieldTemplate
                {
                    FieldConfigId = c.ID,
                    Column = c.COLUMN_NAME,
                    DisplayName = string.IsNullOrWhiteSpace(c.DISPLAY_NAME) ? c.COLUMN_NAME : c.DISPLAY_NAME,
                    DataType = col.DATA_TYPE,

                    ControlType = c.CONTROL_TYPE,
                    DefaultValue = c.QUERY_DEFAULT_VALUE,

                    IsRequired = c.IS_REQUIRED,
                    IsEditable = c.IS_EDITABLE,
                    IsDisplayed = c.IS_DISPLAYED,

                    CanQuery = c.CAN_QUERY,
                    QueryComponent = c.QUERY_COMPONENT,
                    QueryCondition = c.QUERY_CONDITION,

                    ValidationRules = null,

                    IsUseSql = false,
                    DropdownSql = string.Empty,
                    OptionList = new List<FormFieldDropdownOptionsDto>(),
                    SourceTable = null,
                    DetailToRelationDefaultColumn = c.DETAIL_TO_RELATION_DEFAULT_COLUMN
                };
            })
            .ToList();
    }

    private static Dictionary<Guid, Dictionary<string, string>> BuildDropdownOptionMaps(List<FieldTemplate> templates)
    {
        var maps = new Dictionary<Guid, Dictionary<string, string>>();

        foreach (var t in templates)
        {
            if (!t.IsDropdown)
            {
                continue;
            }

            if (t.OptionList.Count == 0)
            {
                continue;
            }

            var map = t.OptionList
                .GroupBy(x => x.OPTION_VALUE?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().OPTION_TEXT ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            maps[t.FieldConfigId] = map;
        }

        return maps;
    }

    // ============================================================
    // Inner types / constants
    // ============================================================

    private sealed class FieldTemplate
    {
        public Guid FieldConfigId { get; set; }
        public string Column { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string DataType { get; set; } = string.Empty;

        public FormControlType ControlType { get; set; }
        public string? DefaultValue { get; set; }

        public bool IsRequired { get; set; }
        public bool IsEditable { get; set; }
        public bool IsDisplayed { get; set; }

        public bool CanQuery { get; set; }
        public QueryComponentType QueryComponent { get; set; }
        public ConditionType QueryCondition { get; set; }

        public List<FormFieldValidationRuleDto>? ValidationRules { get; set; }

        public bool IsUseSql { get; set; }
        public string DropdownSql { get; set; } = string.Empty;
        public List<FormFieldDropdownOptionsDto> OptionList { get; set; } = new();
        public TableSchemaQueryType? SourceTable { get; set; }
        public string? DetailToRelationDefaultColumn { get; set; }

        public bool IsDropdown
        {
            get { return ControlType == FormControlType.Dropdown; }
        }
    }

    private readonly record struct TvfQuery(string Sql, DynamicParameters Parameters);

    private static class ConfigKeys
    {
        public const string FormDesignerRequiredColumns = "FormDesignerSettings:RequiredColumns";
    }

    private static class SqlParamNames
    {
        public const string TvfPrefix = "tvf_p";
        public const string WherePrefix = "w";
        public const string Offset = "offset";
        public const string Fetch = "fetch";
    }

    private sealed class TvfParamRow
    {
        public Guid MasterId { get; set; }
        public string ParameterName { get; set; } = string.Empty;
    }
}
