using DbExtensions.DbExecutor.Interface;
using DcMateClassLibrary.Helper.FormHelper;
using DcMateH5.Abstractions.Form.FormLogic;
using DcMateH5.Abstractions.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DcMateH5.Infrastructure.Form.FormLogic;

public class SchemaService : ISchemaService
{
    private readonly IDbExecutor _dbExecutor;

    public SchemaService(IDbExecutor dbExecutor)
    {
        _dbExecutor = dbExecutor;
    }

    /// <summary>
    /// 非同步取得指定資料表所有欄位名稱。
    /// </summary>
    public Task<List<string>> GetFormFieldMasterAsync(string table, SqlTransaction? tx = null, CancellationToken ct = default)
    {
        const string sql = "/**/SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table";

        if (tx == null)
        {
            return _dbExecutor.QueryAsync<string>(sql, new { table }, ct: ct);
        }

        return _dbExecutor.QueryInTxAsync<string>(tx.Connection!, tx, sql, new { table }, ct: ct);
    }

    /// <summary>
    /// 非同步取得資料表的主鍵欄位名稱（僅限單一主鍵）。
    /// </summary>
    public Task<string?> GetPrimaryKeyColumnAsync(string tableName, CancellationToken ct = default)
    {
        const string sql = @"
SELECT KU.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND TC.TABLE_NAME = @tableName";

        return _dbExecutor.QueryFirstOrDefaultAsync<string>(sql, new { tableName }, ct: ct);
    }

    /// <summary>
    /// 非同步抓取指定 Table 的主鍵欄位名稱集合（忽略大小寫）。
    /// </summary>
    public async Task<HashSet<string>> GetPrimaryKeyColumnsAsync(string tableName, CancellationToken ct = default)
    {
        const string sqlPk = @"/**/
SELECT KU.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND TC.TABLE_NAME = @tableName";

        var rows = await _dbExecutor.QueryAsync<string>(sqlPk, new { tableName }, ct: ct);
        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 非同步查詢主鍵欄位名稱與型別，並將 rawId 轉型為正確型別（支援單一主鍵）。
    /// </summary>
    public async Task<(string PkName, string PkType, object? Value)> ResolvePkAsync(
        string tableName,
        string? rawId,
        SqlTransaction? tx = null,
        CancellationToken ct = default)
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

        var schema = "dbo";
        var pkList = tx == null
            ? await _dbExecutor.QueryAsync<(string Name, string Type)>(sql, new { TableName = tableName, Schema = schema }, ct: ct)
            : await _dbExecutor.QueryInTxAsync<(string Name, string Type)>(tx.Connection!, tx, sql, new { TableName = tableName, Schema = schema }, ct: ct);

        if (!pkList.Any())
            throw new InvalidOperationException($"查無主鍵欄位：{schema}.{tableName}");

        if (pkList.Count > 1)
            throw new NotSupportedException($"目前 ResolvePk 僅支援單一主鍵（實際為 {pkList.Count} 欄）");

        var pk = pkList[0];
        var typedId = rawId != null ? ConvertToColumnTypeHelper.ConvertPkType(rawId, pk.Type) : null;
        return (pk.Name, pk.Type, typedId);
    }

    /// <summary>
    /// 非同步判斷指定資料表欄位是否為 Identity。
    /// </summary>
    public async Task<bool> IsIdentityColumnAsync(
        string tableName,
        string columnName,
        SqlTransaction? tx = null,
        CancellationToken ct = default)
    {
        const string sql = @"
        SELECT COLUMNPROPERTY(
            OBJECT_ID(@TableName),
            @ColumnName,
            'IsIdentity'
        ) AS IsIdentity";

        var isIdentity = tx == null
            ? await _dbExecutor.ExecuteScalarAsync<int>(sql, new { TableName = tableName, ColumnName = columnName }, ct: ct)
            : await _dbExecutor.ExecuteScalarInTxAsync<int>(tx.Connection!, tx, sql, new { TableName = tableName, ColumnName = columnName }, ct: ct);

        return isIdentity == 1;
    }

    /// <summary>
    /// 非同步由 TableId（BASE/DETAIL/VIEW）反查資料表名稱。
    /// </summary>
    public async Task<string> GetTableNameByTableIdAsync(Guid tableId, SqlTransaction? tx = null, CancellationToken ct = default)
    {
        if (tableId == Guid.Empty)
            throw new ArgumentException("tableId 不可為空", nameof(tableId));

        const string sql = @"/**/
SELECT TOP (1)
    CASE
        WHEN BASE_TABLE_ID  = @Id THEN BASE_TABLE_NAME
        WHEN DETAIL_TABLE_ID = @Id THEN DETAIL_TABLE_NAME
        WHEN VIEW_TABLE_ID   = @Id THEN VIEW_TABLE_NAME
        ELSE NULL
    END AS TableName
FROM FORM_FIELD_MASTER
WHERE BASE_TABLE_ID = @Id
   OR DETAIL_TABLE_ID = @Id
   OR VIEW_TABLE_ID = @Id
ORDER BY
    CASE
        WHEN BASE_TABLE_ID  = @Id THEN 1
        WHEN DETAIL_TABLE_ID = @Id THEN 2
        WHEN VIEW_TABLE_ID   = @Id THEN 3
        ELSE 99
    END;";

        var tableName = tx == null
            ? await _dbExecutor.QueryFirstOrDefaultAsync<string>(sql, new { Id = tableId }, ct: ct)
            : await _dbExecutor.QueryFirstOrDefaultInTxAsync<string>(tx.Connection!, tx, sql, new { Id = tableId }, ct: ct);

        tableName = tableName?.Trim();
        if (string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException($"找不到 tableId 對應的資料表名稱：{tableId}");

        return tableName;
    }

    public Task<List<DbColumnInfo>> GetObjectSchemaInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        string schemaName,
        string objectName,
        CancellationToken ct)
    {
        const string sql = @"/**/
SELECT
    c.ORDINAL_POSITION AS [Order],
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE,
    c.CHARACTER_MAXIMUM_LENGTH,
    COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity,
    CASE WHEN pk.COLUMN_NAME IS NULL THEN 0 ELSE 1 END AS IsPk
FROM INFORMATION_SCHEMA.COLUMNS c
LEFT JOIN (
    SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
      ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
     AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
) pk
  ON pk.TABLE_SCHEMA = c.TABLE_SCHEMA
 AND pk.TABLE_NAME = c.TABLE_NAME
 AND pk.COLUMN_NAME = c.COLUMN_NAME
WHERE c.TABLE_SCHEMA = @schemaName
  AND c.TABLE_NAME = @objectName
ORDER BY c.ORDINAL_POSITION";

        return _dbExecutor.QueryInTxAsync<DbColumnInfo>(conn, tx, sql, new { schemaName, objectName }, ct: ct);
    }

    /// <summary>
    /// 同步版本（相容舊呼叫端）。
    /// </summary>
    public List<string> GetFormFieldMaster(string table, SqlTransaction? tx = null)
    {
        const string sql = "/**/SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table";
        return tx == null
            ? _dbExecutor.Query<string>(sql, new { table })
            : _dbExecutor.QueryInTx<string>(tx.Connection!, tx, sql, new { table });
    }

    public string? GetPrimaryKeyColumn(string tableName)
    {
        const string sql = @"
SELECT KU.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND TC.TABLE_NAME = @tableName";
        return _dbExecutor.QueryFirstOrDefault<string>(sql, new { tableName });
    }

    public HashSet<string> GetPrimaryKeyColumns(string tableName)
    {
        const string sqlPk = @"/**/
SELECT KU.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND TC.TABLE_NAME = @tableName";
        return _dbExecutor.Query<string>(sqlPk, new { tableName }).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

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

        var schema = "dbo";
        var pkList = tx == null
            ? _dbExecutor.Query<(string Name, string Type)>(sql, new { TableName = tableName, Schema = schema })
            : _dbExecutor.QueryInTx<(string Name, string Type)>(tx.Connection!, tx, sql, new { TableName = tableName, Schema = schema });

        if (!pkList.Any())
            throw new InvalidOperationException($"查無主鍵欄位：{schema}.{tableName}");

        if (pkList.Count > 1)
            throw new NotSupportedException($"目前 ResolvePk 僅支援單一主鍵（實際為 {pkList.Count} 欄）");

        var pk = pkList[0];
        var typedId = rawId != null ? ConvertToColumnTypeHelper.ConvertPkType(rawId, pk.Type) : null;
        return (pk.Name, pk.Type, typedId);
    }

    public bool IsIdentityColumn(string tableName, string columnName, SqlTransaction? tx = null)
    {
        const string sql = @"
        SELECT COLUMNPROPERTY(
            OBJECT_ID(@TableName),
            @ColumnName,
            'IsIdentity'
        ) AS IsIdentity";

        var isIdentity = tx == null
            ? _dbExecutor.ExecuteScalar<int>(sql, new { TableName = tableName, ColumnName = columnName })
            : _dbExecutor.ExecuteScalarInTx<int>(tx.Connection!, tx, sql, new { TableName = tableName, ColumnName = columnName });

        return isIdentity == 1;
    }

    public string GetTableNameByTableId(Guid tableId, SqlTransaction? tx = null)
    {
        if (tableId == Guid.Empty)
            throw new ArgumentException("tableId 不可為空", nameof(tableId));

        const string sql = @"/**/
SELECT TOP (1)
    CASE
        WHEN BASE_TABLE_ID  = @Id THEN BASE_TABLE_NAME
        WHEN DETAIL_TABLE_ID = @Id THEN DETAIL_TABLE_NAME
        WHEN VIEW_TABLE_ID   = @Id THEN VIEW_TABLE_NAME
        ELSE NULL
    END AS TableName
FROM FORM_FIELD_MASTER
WHERE BASE_TABLE_ID = @Id
   OR DETAIL_TABLE_ID = @Id
   OR VIEW_TABLE_ID = @Id
ORDER BY
    CASE
        WHEN BASE_TABLE_ID  = @Id THEN 1
        WHEN DETAIL_TABLE_ID = @Id THEN 2
        WHEN VIEW_TABLE_ID   = @Id THEN 3
        ELSE 99
    END;";

        var tableName = tx == null
            ? _dbExecutor.QueryFirstOrDefault<string>(sql, new { Id = tableId })
            : _dbExecutor.QueryFirstOrDefaultInTx<string>(tx.Connection!, tx, sql, new { Id = tableId });

        tableName = tableName?.Trim();
        if (string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException($"找不到 tableId 對應的資料表名稱：{tableId}");

        return tableName;
    }

}
