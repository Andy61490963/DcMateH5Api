using ClassLibrary;
using Dapper;
using DcMateH5Api.Helper;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Services.FormLogic;

public class FormDataService : IFormDataService
{
    private readonly SqlConnection _con;
    
    public FormDataService(SqlConnection connection)
    {
        _con = connection;
    }
    
    public List<IDictionary<string, object?>> GetRows(
        string tableName,
        IEnumerable<FormQueryCondition>? conditions = null,
        int? page = null,
        int? pageSize = null)
    {
        var sql   = new System.Text.StringBuilder($"SELECT * FROM [{tableName}]");
        var param = new DynamicParameters();

        if (conditions != null)
        {
            var whereList = new List<string>();
            int i = 0;
            foreach (var c in conditions)
            {
                if (string.IsNullOrWhiteSpace(c.Column))
                    continue;

                // 基礎欄位名稱驗證以避免 SQL Injection
                var column = c.Column;
                if (!System.Text.RegularExpressions.Regex.IsMatch(column, "^[A-Za-z0-9_]+$"))
                    continue;

                // 前端提供 QueryConditionType，映射為 ConditionType
                // var condType = c.QueryConditionType.Value.ToConditionType();
                
                var p1 = $"p{i++}";

                switch (c.ConditionType)
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
                        var val = c.Value != null ? $"%{c.Value}%" : null;
                        param.Add(p1, ConvertToColumnTypeHelper.Convert(c.DataType, val));
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
                        if (c.Values != null && c.Values.Count > 0)
                        {
                            var inParams = new List<string>();
                            foreach (var value in c.Values)
                            {
                                var pIn = $"p{i++}";
                                inParams.Add($"@{pIn}");
                                param.Add(pIn, ConvertToColumnTypeHelper.Convert(c.DataType, value));
                            }
                            whereList.Add($"[{column}] IN ({string.Join(", ", inParams)})");
                        }
                        break;
                        
                    case ConditionType.NotIn:
                        if (c.Values != null && c.Values.Count > 0)
                        {
                            var notInParams = new List<string>();
                            foreach (var value in c.Values)
                            {
                                var pNotIn = $"p{i++}";
                                notInParams.Add($"@{pNotIn}");
                                param.Add(pNotIn, ConvertToColumnTypeHelper.Convert(c.DataType, value));
                            }
                            whereList.Add($"[{column}] NOT IN ({string.Join(", ", notInParams)})");
                        }
                        break;
                }
            }

            if (whereList.Count > 0)
            {
                sql.Append(" WHERE ");
                sql.Append(string.Join(" AND ", whereList));
            }
        }

        // 分頁處理：若有指定 page 與 pageSize，組合 OFFSET / FETCH
        if (page.HasValue && pageSize.HasValue)
        {
            var p  = Math.Max(page.Value, 1);
            var ps = Math.Max(pageSize.Value, 1);
            sql.Append(" ORDER BY (SELECT NULL) OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY");
            param.Add("offset", (p - 1) * ps);
            param.Add("pageSize", ps);
        }

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
}