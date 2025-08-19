using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;

/// <summary>
/// Reflection based implementation of <see cref="ICrudSqlBuilder"/>.
/// Responsible for generating parameterized SQL statements without touching I/O.
/// </summary>
public class CrudSqlBuilder : ICrudSqlBuilder
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertiesCache = new();
    private static readonly Regex _safeIdentRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Validates identifier text and wraps it with brackets to avoid injection.
    /// </summary>
    /// <param name="ident">Identifier string.</param>
    /// <returns>Safe identifier.</returns>
    /// <exception cref="ArgumentException">Thrown when identifier is invalid.</exception>
    public static string SafeIdent(string ident)
    {
        if (string.IsNullOrWhiteSpace(ident) || !_safeIdentRegex.IsMatch(ident))
        {
            throw new ArgumentException($"Invalid identifier '{ident}'.", nameof(ident));
        }
        return $"[{ident}]";
    }

    /// <summary>
    /// Qualifies table name with optional schema.
    /// </summary>
    public string Qualified(string table)
    {
        var parts = table.Split('.');
        var builder = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) builder.Append('.');
            builder.Append(SafeIdent(parts[i]));
        }
        return builder.ToString();
    }

    /// <inheritdoc />
    public (string Sql, DynamicParameters Params) BuildInsert(string table, object dto)
    {
        try
        {
            var props = GetProperties(dto);
            var columns = string.Join(",", props.Select(p => SafeIdent(p.Name)));
            var parameters = string.Join(",", props.Select(p => "@" + p.Name));
            var sql = $"INSERT INTO {Qualified(table)} ({columns}) VALUES ({parameters});";
            var dp = new DynamicParameters(dto);
            return (sql, dp);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to build INSERT SQL.", ex);
        }
    }

    /// <inheritdoc />
    public (string Sql, DynamicParameters Params) BuildInsertOutput(string table, object dto, string identityColumn)
    {
        try
        {
            var props = GetProperties(dto);
            var columns = string.Join(",", props.Select(p => SafeIdent(p.Name)));
            var parameters = string.Join(",", props.Select(p => "@" + p.Name));
            var sql = $"INSERT INTO {Qualified(table)} ({columns}) OUTPUT INSERTED.{SafeIdent(identityColumn)} VALUES ({parameters});";
            var dp = new DynamicParameters(dto);
            return (sql, dp);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to build INSERT SQL.", ex);
        }
    }

    /// <inheritdoc />
    public (string Sql, DynamicParameters Params) BuildUpdate(string table, object setDto, object whereDto)
    {
        try
        {
            var dp = new DynamicParameters();
            var setClause = BuildSet(setDto, dp);
            var whereClause = BuildWhere(whereDto, dp);
            var sql = $"UPDATE {Qualified(table)} SET {setClause} WHERE {whereClause};";
            return (sql, dp);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to build UPDATE SQL.", ex);
        }
    }

    /// <inheritdoc />
    public (string Sql, DynamicParameters Params) BuildDelete(string table, object whereDto)
    {
        try
        {
            var dp = new DynamicParameters();
            var whereClause = BuildWhere(whereDto, dp);
            var sql = $"DELETE FROM {Qualified(table)} WHERE {whereClause};";
            return (sql, dp);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to build DELETE SQL.", ex);
        }
    }

    /// <inheritdoc />
    public (string Sql, DynamicParameters Params) BuildExists(string table, object whereDto)
    {
        try
        {
            var dp = new DynamicParameters();
            var whereClause = BuildWhere(whereDto, dp);
            var sql = $"SELECT 1 FROM {Qualified(table)} WHERE {whereClause};";
            return (sql, dp);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to build EXISTS SQL.", ex);
        }
    }

    private static string BuildSet(object setDto, DynamicParameters parameters)
    {
        var props = GetProperties(setDto);
        if (props.Length == 0)
        {
            throw new ArgumentException("setDto has no properties.", nameof(setDto));
        }
        var assignments = new List<string>(props.Length);
        foreach (var p in props)
        {
            var paramName = $"set_{p.Name}";
            assignments.Add($"{SafeIdent(p.Name)}=@{paramName}");
            parameters.Add(paramName, p.GetValue(setDto));
        }
        return string.Join(",", assignments);
    }

    /// <summary>
    /// Builds WHERE clause and appends parameters with <c>w_</c> prefix.
    /// </summary>
    private static string BuildWhere(object whereDto, DynamicParameters parameters)
    {
        var props = GetProperties(whereDto);
        if (props.Length == 0)
        {
            throw new ArgumentException("whereDto has no properties.", nameof(whereDto));
        }
        var conditions = new List<string>(props.Length);
        foreach (var p in props)
        {
            var paramName = $"w_{p.Name}";
            conditions.Add($"{SafeIdent(p.Name)}=@{paramName}");
            parameters.Add(paramName, p.GetValue(whereDto));
        }
        return string.Join(" AND ", conditions);
    }

    private static PropertyInfo[] GetProperties(object dto)
    {
        var type = dto.GetType();
        return _propertiesCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public));
    }
}
