using Dapper;

/// <summary>
/// Builds parameterized SQL statements for basic CRUD operations.
/// </summary>
public interface ICrudSqlBuilder
{
    /// <summary>
    /// Builds an INSERT statement.
    /// </summary>
    /// <param name="table">Target table name.</param>
    /// <param name="dto">DTO containing column values.</param>
    /// <returns>SQL text and parameters.</returns>
    (string Sql, DynamicParameters Params) BuildInsert(string table, object dto);

    /// <summary>
    /// Builds an INSERT statement that outputs the generated identity value.
    /// </summary>
    /// <typeparam name="T">Type of the identity column.</typeparam>
    /// <param name="table">Target table name.</param>
    /// <param name="dto">DTO containing column values.</param>
    /// <param name="identityColumn">Identity column name to return.</param>
    /// <returns>SQL text and parameters.</returns>
    (string Sql, DynamicParameters Params) BuildInsertOutput(string table, object dto, string identityColumn);

    /// <summary>
    /// Builds an UPDATE statement with a WHERE clause.
    /// </summary>
    /// <param name="table">Target table name.</param>
    /// <param name="setDto">DTO containing values to update.</param>
    /// <param name="whereDto">DTO describing WHERE conditions. Supports composite keys via property names.</param>
    /// <returns>SQL text and parameters.</returns>
    (string Sql, DynamicParameters Params) BuildUpdate(string table, object setDto, object whereDto);

    /// <summary>
    /// Builds a DELETE statement.
    /// </summary>
    /// <param name="table">Target table name.</param>
    /// <param name="whereDto">DTO describing WHERE conditions. Supports composite keys via property names.</param>
    /// <returns>SQL text and parameters.</returns>
    (string Sql, DynamicParameters Params) BuildDelete(string table, object whereDto);

    /// <summary>
    /// Builds an EXISTS statement returning 1 when rows satisfying the condition exist.
    /// </summary>
    /// <param name="table">Target table name.</param>
    /// <param name="whereDto">DTO describing WHERE conditions. Supports composite keys via property names.</param>
    /// <returns>SQL text and parameters.</returns>
    (string Sql, DynamicParameters Params) BuildExists(string table, object whereDto);
}
