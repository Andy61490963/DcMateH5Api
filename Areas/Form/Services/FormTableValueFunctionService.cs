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
    public IEnumerable<TableValueFunctionConfigViewModel> GetFormMasters(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        const string sql = @"
/**/
SELECT 
    M.ID             AS Id,
    M.FORM_NAME      AS FormName,
    M.TVF_TABLE_ID   AS TableFunctionValueId,
    M.TVF_TABLE_NAME AS TableFunctionValueName
FROM FORM_FIELD_MASTER M
WHERE M.FUNCTION_TYPE = @funcType
  AND M.IS_DELETE = 0;

/**/
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

        using var grid = _con.QueryMultiple(new CommandDefinition(sql, args, cancellationToken: ct));

        var masters = grid.Read<TableValueFunctionConfigViewModel>().ToList();

        var paramRows = grid.Read<TvfParamRow>().ToList();

        var paramMap = paramRows
            .GroupBy(x => x.MasterId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ParameterName).Where(s => !string.IsNullOrWhiteSpace(s)).ToList());

        foreach (var m in masters)
        {
            if (paramMap.TryGetValue(m.TableFunctionValueId, out var list))
            {
                m.Parameter = list;
            }
            else
            {
                m.Parameter = new List<string>();
            }
        }

        return masters;
    }

    /// <summary>
    /// 取得 TVF 表單列表頁所需的資料清單（含各欄位實際值），
    /// 並將 Dropdown 欄位的選項值（OptionId）轉換為顯示文字（OptionText）。
    /// </summary>
    /// <param name="funcType">表單功能類型</param>
    /// <param name="request">查詢條件與分頁資訊（可選）</param>
    public List<FormTvfListDataViewModel> GetTvfFormList(FormFunctionType funcType, FormTvfSearchRequest? request = null)
    {
        // ------------------------------------------------------------
        // 0. 入參防呆（分頁 / 條件 / 排序 / TVF params）
        // ------------------------------------------------------------
        var page = request?.Page ?? 0;
        var pageSize = request?.PageSize ?? 20;

        var conditions = request?.Conditions ?? new List<FormTvfQueryConditionViewModel>();
        var orderBys = request?.OrderBys;

        // CHANGED: 先拿到原始 TvfParameters（可能為 null）
        var tvfParamKvsFromRequest = request?.TvfParameters;

        // CHANGED: request 有帶 FormMasterId（必填），用它篩選 metas，避免原本掃全部 master
        var requestMasterId = request?.FormMasterId ?? Guid.Empty;

        // ------------------------------------------------------------
        // 1. 取得表單主設定（含欄位設定）
        // ------------------------------------------------------------
        var metas = _formFieldMasterService.GetFormMetaAggregates(
            funcType,
            TableSchemaQueryType.All
        );

        // CHANGED: 如果有指定 masterId，僅處理該 master（符合 request 期待）
        if (requestMasterId != Guid.Empty)
        {
            metas = metas
                .Where(x => x.Master.ID == requestMasterId)
                .ToList();
        }

        var results = new List<FormTvfListDataViewModel>();

        foreach (var (master, _) in metas)
        {
            var tvfName = master.TVF_TABLE_NAME;
            if (string.IsNullOrWhiteSpace(tvfName))
            {
                continue;
            }

            if (!SafeSqlIdentifierRegex.IsMatch(tvfName))
            {
                continue;
            }

            // configs：抓完整 config（不改 schema、不加套件，維持 Dapper）
            var fieldConfigs = LoadFieldConfigs(master.TVF_TABLE_ID);

            // CHANGED: TvfParameters == null 時，自動帶入 FORM_FIELD_CONFIG 的 TVF_CURRENT_VALUE
            //          若找不到任何 IS_TVF_QUERY_PARAMETER=1 → 丟 400（你指定）
            var tvfParamKvs = ResolveTvfParametersOrThrow(master.ID, fieldConfigs, tvfParamKvsFromRequest);

            // --------------------------------------------------------
            // 2. schema + query：同一個交易內完成（你要求 schema tx 版）
            // --------------------------------------------------------
            var rows = _txService.WithTransaction(tx =>
            {
                var schema = _schemaService
                    .GetObjectSchemaInTxAsync(_con, tx, DefaultSchemaName, tvfName, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

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

                var templates = BuildFieldTemplates(fieldConfigs, returnColumns);
                if (templates.Count == 0)
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

                return ExecuteRowsInTx(_con, tx, query.Sql, query.Parameters);
            });

            if (rows.Count == 0)
            {
                continue;
            }

            var templatesForVm = BuildFieldTemplates(fieldConfigs, GetReturnColumnsFromConfigs(fieldConfigs));
            var dropdownMaps = BuildDropdownOptionMaps(templatesForVm);

            foreach (var row in rows)
            {
                var rowFields = templatesForVm
                    .Select(t =>
                    {
                        row.TryGetValue(t.Column, out var currentValue);

                        if (t.IsDropdown && dropdownMaps.TryGetValue(t.FieldConfigId, out var map))
                        {
                            var key = currentValue?.ToString();
                            if (!string.IsNullOrWhiteSpace(key) && map.TryGetValue(key, out var text))
                            {
                                currentValue = text;
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
                            IS_PK = false,
                            IS_RELATION = false,
                            ValidationRules = t.ValidationRules,
                            ISUSESQL = t.IsUseSql,
                            DROPDOWNSQL = t.DropdownSql,
                            OptionList = t.OptionList,
                            SOURCE_TABLE = t.SourceTable,
                            CurrentValue = currentValue,
                            DETAIL_TO_RELATION_DEFAULT_COLUMN = t.DetailToRelationDefaultColumn
                        };
                    })
                    .ToList();

                results.Add(new FormTvfListDataViewModel
                {
                    FormMasterId = master.ID,
                    FormName = master.FORM_NAME,
                    Fields = rowFields
                });
            }
        }

        return results;
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
            // CHANGED: 統一正規化 key，移除開頭 '@'
            var key = NormalizeTvfParamName(c.COLUMN_NAME);

            // CHANGED: 用正規化後的 key 做防呆（避免 '@P' / 'P' 重複）
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

            // 依你定義：value = TVF_CURRENT_VALUE（nvarchar）
            dict[key] = c.TVF_CURRENT_VALUE;
        }

        return dict;
    }

    // ============================================================
    // configs
    // ============================================================

    private List<FormFieldConfigDto> LoadFieldConfigs(Guid? masterId)
    {
        EnsureConnectionOpened(_con);

        var sql = @"
/**/
SELECT *
FROM FORM_FIELD_CONFIG
WHERE FORM_FIELD_MASTER_ID = @MasterId
ORDER BY FIELD_ORDER;";

        return _con.Query<FormFieldConfigDto>(sql, new { MasterId = masterId }).ToList();
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

    private static void EnsureConnectionOpened(SqlConnection con)
    {
        if (con.State != ConnectionState.Open)
        {
            con.Open();
        }
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
    // Execute（tx 內，reader -> Dictionary）
    // ============================================================

    private static List<IDictionary<string, object?>> ExecuteRowsInTx(
        SqlConnection conn,
        SqlTransaction tx,
        string sql,
        DynamicParameters param)
    {
        using var reader = conn.ExecuteReader(new CommandDefinition(sql, param, transaction: tx));

        var rows = new List<IDictionary<string, object?>>();

        while (reader.Read())
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                dict[name] = value;
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
