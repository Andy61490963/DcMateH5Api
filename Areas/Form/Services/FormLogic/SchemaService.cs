using ClassLibrary;
using Dapper;
using DynamicForm.Helper;
using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.Interfaces.FormLogic;
using Microsoft.Data.SqlClient;

namespace DynamicForm.Areas.Form.Services.FormLogic;

public class SchemaService : ISchemaService
{
    private readonly SqlConnection _con;
    
    public SchemaService(SqlConnection connection)
    {
        _con = connection;
    }

    public List<string> GetFormFieldMaster(string table)
    {
        return _con.Query<string>(
            "/**/SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table",
            new { table }).ToList();
    }

    /// <summary>
    /// 取得資料表的主鍵欄位名稱（僅限單一主鍵）
    /// </summary>
    /// <param name="tableName">資料表名稱</param>
    /// <returns>主鍵欄位名稱，若無主鍵則為 null</returns>
    public string? GetPrimaryKeyColumn(string tableName)
    {
        const string sql = @"
SELECT KU.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND TC.TABLE_NAME = @tableName";

        return _con.QueryFirstOrDefault<string>(sql, new { tableName });
    }
    
    /// <summary>抓取指定 Table 的主鍵欄位名稱集合 (忽略大小寫)</summary>
    public HashSet<string> GetPrimaryKeyColumns(string tableName)
    {
        const string sqlPk = @"/**/
SELECT KU.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND TC.TABLE_NAME = @tableName";

        return _con.Query<string>(sqlPk, new { tableName })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 查詢主鍵欄位名稱、型別，並將 id 轉型成正確型別
    /// </summary>
    /// <summary>
    /// 查詢資料表實體主鍵欄位名稱與型別，並將 rawId 轉型為正確型別（支援單一主鍵）
    /// </summary>
    /// <param name="tableName">表單主檔</param>
    /// <param name="rawId">原始主鍵字串</param>
    /// <returns>主鍵名稱、型別與轉型後的值</returns>
    public (string PkName, string PkType, object? Value) ResolvePk(string tableName, string? rawId, SqlTransaction? tx = null)
    {
        const string sql = @"/**/
        SELECT col.COLUMN_NAME AS Name, col.DATA_TYPE AS Type
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS pk
        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu 
            ON pk.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND pk.TABLE_SCHEMA = kcu.TABLE_SCHEMA
        JOIN INFORMATION_SCHEMA.COLUMNS col
            ON col.TABLE_NAME = kcu.TABLE_NAME 
            AND col.COLUMN_NAME = kcu.COLUMN_NAME 
            AND col.TABLE_SCHEMA = kcu.TABLE_SCHEMA
        WHERE pk.CONSTRAINT_TYPE = 'PRIMARY KEY'
          AND pk.TABLE_NAME = @TableName
          AND pk.TABLE_SCHEMA = ISNULL(@Schema, 'dbo')
        ORDER BY kcu.ORDINAL_POSITION";

        // 自動取得 schema 名稱（若 FORM_FIELD_Master 有的話）
        var schema = "dbo";

        var pkList = _con.Query<(string Name, string Type)>(sql, new { TableName = tableName, Schema = schema }, transaction: tx).ToList();

        if (!pkList.Any())
            throw new InvalidOperationException($"查無主鍵欄位：{schema}.{tableName}");

        if (pkList.Count > 1)
            throw new NotSupportedException($"目前 ResolvePk 僅支援單一主鍵（實際為 {pkList.Count} 欄）");

        var pk = pkList.First();

        var typedId = rawId != null
            ? ConvertToColumnTypeHelper.ConvertPkType(rawId, pk.Type)
            : null;

        return (pk.Name, pk.Type, typedId);
    }
    
    /// <summary>
    /// 判斷指定的資料表欄位是否為 Identity（自動遞增主鍵）
    /// </summary>
    /// <param name="tableName">資料表名稱（建議含 schema，例如 dbo.Users）</param>
    /// <param name="columnName">欄位名稱</param>
    /// <returns>true：為 Identity；false：非 Identity 或查無資料</returns>
    public bool IsIdentityColumn(string tableName, string columnName, SqlTransaction? tx = null)
    {
        var sql = @"
        SELECT COLUMNPROPERTY(
            OBJECT_ID(@TableName), 
            @ColumnName, 
            'IsIdentity'
        ) AS IsIdentity";

        var isIdentity = _con.ExecuteScalar<int>(sql, new
        {
            TableName = tableName,
            ColumnName = columnName
        }, transaction: tx);

        return isIdentity == 1;
    }
}