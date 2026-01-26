using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Services;

/// <summary>
/// 根據 SQL 查詢結果同步下拉選項，並維持資料表與來源資料一致。
/// </summary>
public class DropdownSqlSyncService : IDropdownSqlSyncService
{
    private readonly SqlConnection _con;

    private const string ExistingOptionsSql =
        "SELECT ID, OPTION_VALUE, OPTION_TEXT, OPTION_TABLE, IS_DELETE FROM FORM_FIELD_DROPDOWN_OPTIONS WHERE FORM_FIELD_DROPDOWN_ID = @DropdownId";

    public const string UpsertDropdownOption = @"/**/
MERGE dbo.FORM_FIELD_DROPDOWN_OPTIONS AS target
USING (
    SELECT
        @Id             AS ID,                 -- Guid (可能是空)
        @DropdownId     AS FORM_FIELD_DROPDOWN_ID,
        @OptionText     AS OPTION_TEXT,
        @OptionValue    AS OPTION_VALUE,
        @OptionTable    AS OPTION_TABLE
) AS source
ON target.ID = source.ID                     -- 只比對 PK
WHEN MATCHED THEN
    UPDATE SET
        OPTION_TEXT  = source.OPTION_TEXT,
        OPTION_VALUE = source.OPTION_VALUE,
        OPTION_TABLE = source.OPTION_TABLE,
        IS_DELETE    = 0
WHEN NOT MATCHED THEN
    INSERT (ID, FORM_FIELD_DROPDOWN_ID, OPTION_TEXT, OPTION_VALUE, OPTION_TABLE, IS_DELETE)
    VALUES (ISNULL(source.ID, NEWID()),       -- 若 Guid.Empty → 直接 NEWID()
            source.FORM_FIELD_DROPDOWN_ID,
            source.OPTION_TEXT,
            source.OPTION_VALUE,
            source.OPTION_TABLE,
            0)
OUTPUT INSERTED.ID;                          -- 把 ID 回傳給 Dapper
";
    
    public DropdownSqlSyncService(SqlConnection connection)
    {
        _con = connection;
    }

    public DropdownSqlSyncResult Sync(Guid dropdownId, string sql, SqlTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new DropdownSqlSyncException("SQL 不可為空白。");

        var optionTable = TryExtractTableName(sql)
                         ?? "自訂的下拉選單";

        var ownTransaction = transaction is null;
        var shouldCloseConnection = false;
        var tx = transaction;

        try
        {
            if (ownTransaction)
            {
                if (_con.State != ConnectionState.Open)
                {
                    _con.Open();
                    shouldCloseConnection = true;
                }

                tx = _con.BeginTransaction();
            }

            var (rows, preview) = ExecuteSql(sql, tx!);
            var normalizedTable = NormalizeOptionTable(optionTable);

            var existing = _con.Query<DropdownOptionDbRow>(ExistingOptionsSql, new { DropdownId = dropdownId }, tx)
                .Where(x => NormalizeOptionTable(x.OPTION_TABLE) == normalizedTable)
                .ToDictionary(x => x.OPTION_VALUE, x => x);

            var duplicateGuard = new HashSet<string>();
            var result = new DropdownSqlSyncResult
            {
                RowCount = rows.Count,
                PreviewRows = preview
            };

            foreach (var row in rows)
            {
                if (!duplicateGuard.Add(row.OptionValue))
                    throw new DropdownSqlSyncException($"第 {row.RowNumber} 筆資料的 ID 與其他列重複。");

                existing.TryGetValue(row.OptionValue, out var existingRow);
                existing.Remove(row.OptionValue);

                var upsertId = _con.ExecuteScalar<Guid>(UpsertDropdownOption, new
                {
                    Id = existingRow?.ID,
                    DropdownId = dropdownId,
                    OptionText = row.OptionText,
                    OptionValue = row.OptionValue,
                    OptionTable = optionTable
                }, tx);

                result.Options.Add(new FormFieldDropdownOptionsDto
                {
                    ID = upsertId,
                    FORM_FIELD_DROPDOWN_ID = dropdownId,
                    OPTION_TABLE = optionTable,
                    OPTION_VALUE = row.OptionValue,
                    OPTION_TEXT = row.OptionText
                });
            }

            if (existing.Count > 0)
            {
                var staleIds = existing.Values.Select(x => x.ID).ToList();
                _con.Execute(
                    "UPDATE FORM_FIELD_DROPDOWN_OPTIONS SET IS_DELETE = 1 WHERE ID IN @Ids",
                    new { Ids = staleIds },
                    tx);
            }

            if (ownTransaction)
                tx!.Commit();

            return result;
        }
        catch
        {
            if (ownTransaction)
                tx?.Rollback();
            throw;
        }
        finally
        {
            if (ownTransaction && shouldCloseConnection)
                _con.Close();
        }
    }

    private (List<DropdownSqlRow> Rows, List<Dictionary<string, object?>> Preview) ExecuteSql(string sql, SqlTransaction transaction)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = transaction;

        using var reader = cmd.ExecuteReader();
        var schema = reader.GetColumnSchema();

        if (schema.Count < 2)
            throw new DropdownSqlSyncException("SQL 必須至少回傳兩個欄位，並且需包含 ID 與 NAME。");

        var idColumn = FindColumn(schema, "ID")
                       ?? throw new DropdownSqlSyncException("查詢結果必須包含 ID 欄位別名。");
        var nameColumn = FindColumn(schema, "NAME")
                         ?? throw new DropdownSqlSyncException("查詢結果必須包含 NAME 欄位別名。");

        var rows = new List<DropdownSqlRow>();
        var preview = new List<Dictionary<string, object>>();
        var rowNumber = 0;

        while (reader.Read())
        {
            rowNumber++;

            var optionValue = ReadRequiredString(reader, idColumn, rowNumber, "ID");
            var optionText = ReadRequiredString(reader, nameColumn, rowNumber, "NAME");

            rows.Add(new DropdownSqlRow(rowNumber, optionValue, optionText));

            if (preview.Count < 10)
            {
                var rowPreview = new Dictionary<string, object>(schema.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var column in schema)
                {
                    var ordinal = column.ColumnOrdinal ?? throw new DropdownSqlSyncException("查詢結果缺少欄位序號資訊。");
                    rowPreview[column.ColumnName] = reader.IsDBNull(ordinal)
                        ? null
                        : reader.GetValue(ordinal);
                }
                preview.Add(rowPreview);
            }
        }

        return (rows, preview);
    }

    private static string ReadRequiredString(SqlDataReader reader, DbColumn column, int rowNumber, string columnAlias)
    {
        var ordinal = column.ColumnOrdinal ?? throw new DropdownSqlSyncException("查詢結果缺少欄位序號資訊。");
        if (reader.IsDBNull(ordinal))
            throw new DropdownSqlSyncException($"第 {rowNumber} 筆資料的 {columnAlias} 為 NULL。");

        var value = Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? string.Empty;
        if (value.Length == 0)
            throw new DropdownSqlSyncException($"第 {rowNumber} 筆資料的 {columnAlias} 為空字串。");

        return value;
    }

    private static DbColumn? FindColumn(IReadOnlyList<DbColumn> schema, string alias) =>
        schema.FirstOrDefault(c => c.ColumnName.Equals(alias, StringComparison.OrdinalIgnoreCase));

    private static string? TryExtractTableName(string sql)
    {
        var match = Regex.Match(sql,
            @"from\s+(?:\[(?<schema>[^\]]+)\]\.)?\[?(?<table>[A-Za-z0-9_]+)\]?",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["table"].Value : null;
    }

    private static string NormalizeOptionTable(string? table) =>
        string.IsNullOrWhiteSpace(table) ? string.Empty : table.Trim().ToLowerInvariant();

    private sealed record DropdownSqlRow(int RowNumber, string OptionValue, string OptionText);

    private sealed class DropdownOptionDbRow
    {
        public Guid ID { get; set; }
        public string OPTION_VALUE { get; set; } = string.Empty;
        public string OPTION_TEXT { get; set; } = string.Empty;
        public string? OPTION_TABLE { get; set; }
        public bool IS_DELETE { get; set; }
    }
}

public class DropdownSqlSyncException : Exception
{
    public DropdownSqlSyncException(string message) : base(message)
    {
    }
}
