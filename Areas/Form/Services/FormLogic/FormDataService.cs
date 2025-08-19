using ClassLibrary;
using Dapper;
using DynamicForm.Helper;
using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.Interfaces.FormLogic;
using Microsoft.Data.SqlClient;

namespace DynamicForm.Areas.Form.Services.FormLogic;

public class FormDataService : IFormDataService
{
    private readonly SqlConnection _con;
    
    public FormDataService(SqlConnection connection)
    {
        _con = connection;
    }
    
    public List<IDictionary<string, object?>> GetRows(string tableName, IEnumerable<FormQueryCondition>? conditions = null)
    {
        var sql    = new System.Text.StringBuilder($"SELECT * FROM [{tableName}]");
        var param  = new DynamicParameters();

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
                var condType = c.QueryConditionType.Value.ToConditionType();
                
                var p1 = $"p{i++}";

                switch (condType)
                {
                    case ConditionType.Equal:
                        whereList.Add($"[{column}] = @{p1}");
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
                }
            }

            if (whereList.Count > 0)
            {
                sql.Append(" WHERE ");
                sql.Append(string.Join(" AND ", whereList));
            }
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