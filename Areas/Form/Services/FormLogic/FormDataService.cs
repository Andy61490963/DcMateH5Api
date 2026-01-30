using System.Text;
using System.Text.RegularExpressions;
using ClassLibrary;
using Dapper;
using DcMateH5Api.Helper;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Services.FormLogic;

public class FormDataService : IFormDataService
{
    private static readonly Regex SafeSqlIdentifierRegex
        = new("^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly SqlConnection _con;

    public FormDataService(SqlConnection connection)
    {
        _con = connection;
    }

    public List<IDictionary<string, object?>> GetRows(
        string tableName,
        IEnumerable<FormQueryConditionViewModel>? conditions = null,
        IEnumerable<FormOrderBy>? orderBys = null,
        int? page = null,
        int? pageSize = null)
    {
        // tableName 也是 injection 入口，至少做基本驗證（你目前是直接塞進 []）
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("tableName 不可為空", nameof(tableName));
        }

        if (!SafeSqlIdentifierRegex.IsMatch(tableName))
        {
            throw new ArgumentException("tableName 含非法字元", nameof(tableName));
        }

        var sql = new StringBuilder($"SELECT * FROM [{tableName}]");
        var param = new DynamicParameters();

        AppendWhere(sql, param, conditions);
        AppendOrderBy(sql, orderBys, page, pageSize);
        AppendPaging(sql, param, page, pageSize);

        var rows = _con.Query(sql.ToString(), param);
        return rows.Cast<IDictionary<string, object?>>().ToList();
    }

    public Dictionary<string, string> LoadColumnTypes(string tableName)
    {
        return _con.Query<(string COLUMN_NAME, string DATA_TYPE)>(
                @"/**/SELECT COLUMN_NAME, DATA_TYPE
                  FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_NAME = @TableName",
                new { TableName = tableName })
            .ToDictionary(x => x.COLUMN_NAME, x => x.DATA_TYPE, StringComparer.OrdinalIgnoreCase);
    }

    private static void AppendWhere(
        StringBuilder sql,
        DynamicParameters param,
        IEnumerable<FormQueryConditionViewModel>? conditions)
    {
        if (conditions == null)
        {
            return;
        }

        var whereList = new List<string>();
        var i = 0;

        foreach (var c in conditions)
        {
            var column = NormalizeAndValidateColumn(c.Column);
            if (column == null)
            {
                continue;
            }

            // 沒給 ConditionType 的話，維持既有行為：不加條件（不擅自預設 Equal，避免改行為）
            if (c.ConditionType == null)
            {
                continue;
            }

            var p1 = $"p{i++}";

            switch (c.ConditionType.Value)
            {
                case ConditionType.Equal:
                    whereList.Add($"[{column}] = @{p1}");
                    param.Add(p1, ConvertToColumnTypeHelper.Convert(c.DataType, c.Value));
                    break;

                case ConditionType.NotEqual:
                    whereList.Add($"[{column}] <> @{p1}");
                    param.Add(p1, ConvertToColumnTypeHelper.Convert(c.DataType, c.Value));
                    break;

                case ConditionType.Like:
                    whereList.Add($"[{column}] LIKE @{p1}");
                    var likeVal = c.Value != null ? $"%{c.Value}%" : null;
                    param.Add(p1, ConvertToColumnTypeHelper.Convert(c.DataType, likeVal));
                    break;

                case ConditionType.Between:
                    var p2 = $"p{i++}";
                    whereList.Add($"[{column}] BETWEEN @{p1} AND @{p2}");
                    param.Add(p1, ConvertToColumnTypeHelper.Convert(c.DataType, c.Value));
                    param.Add(p2, ConvertToColumnTypeHelper.Convert(c.DataType, c.Value2));
                    break;

                case ConditionType.GreaterThan:
                    whereList.Add($"[{column}] > @{p1}");
                    param.Add(p1, ConvertToColumnTypeHelper.Convert(c.DataType, c.Value));
                    break;

                case ConditionType.GreaterThanOrEqual:
                    whereList.Add($"[{column}] >= @{p1}");
                    param.Add(p1, ConvertToColumnTypeHelper.Convert(c.DataType, c.Value));
                    break;

                case ConditionType.LessThan:
                    whereList.Add($"[{column}] < @{p1}");
                    param.Add(p1, ConvertToColumnTypeHelper.Convert(c.DataType, c.Value));
                    break;

                case ConditionType.LessThanOrEqual:
                    whereList.Add($"[{column}] <= @{p1}");
                    param.Add(p1, ConvertToColumnTypeHelper.Convert(c.DataType, c.Value));
                    break;

                case ConditionType.In:
                    AppendInClause(whereList, param, column, c, ref i, isNotIn: false);
                    break;

                case ConditionType.NotIn:
                    AppendInClause(whereList, param, column, c, ref i, isNotIn: true);
                    break;
            }
        }

        if (whereList.Count <= 0)
        {
            return;
        }

        sql.Append(" WHERE ");
        sql.Append(string.Join(" AND ", whereList));
    }

    private static void AppendInClause(
        List<string> whereList,
        DynamicParameters param,
        string column,
        FormQueryConditionViewModel c,
        ref int i,
        bool isNotIn)
    {
        if (c.Values == null || c.Values.Count <= 0)
        {
            return;
        }

        var inParams = new List<string>(capacity: c.Values.Count);

        foreach (var value in c.Values)
        {
            var p = $"p{i++}";
            inParams.Add($"@{p}");
            param.Add(p, ConvertToColumnTypeHelper.Convert(c.DataType, value));
        }

        var op = isNotIn ? "NOT IN" : "IN";
        whereList.Add($"[{column}] {op} ({string.Join(", ", inParams)})");
    }

    private static void AppendOrderBy(
        StringBuilder sql,
        IEnumerable<FormOrderBy>? orderBys,
        int? page,
        int? pageSize)
    {
        // 只有在需要分頁時，才「必須」有 ORDER BY
        // 但你原本是 (SELECT NULL)，所以我們維持行為：沒給排序也照樣補一個
        var needOrderBy = page.HasValue && pageSize.HasValue;

        if (!needOrderBy)
        {
            // 不分頁：只有在有指定 orderBys 才加 ORDER BY（避免改行為）
            if (orderBys == null)
            {
                return;
            }
        }

        var clauses = BuildOrderByClauses(orderBys);

        if (clauses.Count <= 0)
        {
            // 分頁且沒有合法排序：維持你現在的行為
            sql.Append(" ORDER BY (SELECT NULL)");
            return;
        }

        sql.Append(" ORDER BY ");
        sql.Append(string.Join(", ", clauses));
    }

    private static List<string> BuildOrderByClauses(IEnumerable<FormOrderBy>? orderBys)
    {
        var clauses = new List<string>();

        if (orderBys == null)
        {
            return clauses;
        }

        foreach (var ob in orderBys)
        {
            var column = NormalizeAndValidateColumn(ob.Column);
            if (column == null)
            {
                continue;
            }

            var dir = ob.Direction == SortType.Desc ? "DESC" : "ASC";
            clauses.Add($"[{column}] {dir}");
        }

        return clauses;
    }

    private static void AppendPaging(
        StringBuilder sql,
        DynamicParameters param,
        int? page,
        int? pageSize)
    {
        if (!page.HasValue || !pageSize.HasValue)
        {
            return;
        }

        var p = Math.Max(page.Value, 1);
        var ps = Math.Max(pageSize.Value, 1);

        sql.Append(" OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY");
        param.Add("offset", (p - 1) * ps);
        param.Add("pageSize", ps);
    }

    private static string? NormalizeAndValidateColumn(string? column)
    {
        if (string.IsNullOrWhiteSpace(column))
        {
            return null;
        }

        // 你原本也只允許 [A-Za-z0-9_]
        if (!SafeSqlIdentifierRegex.IsMatch(column))
        {
            return null;
        }

        return column;
    }
}
