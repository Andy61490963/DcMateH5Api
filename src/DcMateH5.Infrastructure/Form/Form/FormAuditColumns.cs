using Dapper;

namespace DcMateH5.Infrastructure.Form.Form;

internal static class FormAuditColumns
{
    public const string CreateUser = "CREATE_USER";
    public const string CreateTime = "CREATE_TIME";
    public const string EditUser = "EDIT_USER";
    public const string EditTime = "EDIT_TIME";

    public static bool IsAuditColumn(string columnName)
    {
        return string.Equals(columnName, CreateUser, StringComparison.OrdinalIgnoreCase)
               || string.Equals(columnName, CreateTime, StringComparison.OrdinalIgnoreCase)
               || string.Equals(columnName, EditUser, StringComparison.OrdinalIgnoreCase)
               || string.Equals(columnName, EditTime, StringComparison.OrdinalIgnoreCase);
    }

    public static void AddInsertColumns(
        HashSet<string> tableColumns,
        List<string> columns,
        List<string> values,
        IDictionary<string, object> parameters,
        string account)
    {
        AddInsertColumn(tableColumns, columns, values, parameters, CreateUser, "AuditCreateUser", account);
        AddInsertColumn(tableColumns, columns, values, parameters, CreateTime, null, null, "SYSDATETIME()");
        AddInsertColumn(tableColumns, columns, values, parameters, EditUser, "AuditEditUser", account);
        AddInsertColumn(tableColumns, columns, values, parameters, EditTime, null, null, "SYSDATETIME()");
    }

    public static void AddInsertColumns(
        HashSet<string> tableColumns,
        List<string> columns,
        List<string> values,
        DynamicParameters parameters,
        string account)
    {
        AddInsertColumn(tableColumns, columns, values, parameters, CreateUser, "AuditCreateUser", account);
        AddInsertColumn(tableColumns, columns, values, parameters, CreateTime, null, null, "SYSDATETIME()");
        AddInsertColumn(tableColumns, columns, values, parameters, EditUser, "AuditEditUser", account);
        AddInsertColumn(tableColumns, columns, values, parameters, EditTime, null, null, "SYSDATETIME()");
    }

    public static void AddUpdateColumns(
        HashSet<string> tableColumns,
        List<string> setList,
        IDictionary<string, object> parameters,
        string account)
    {
        AddUpdateColumn(tableColumns, setList, parameters, EditUser, "AuditEditUser", account);
        AddUpdateColumn(tableColumns, setList, parameters, EditTime, null, null, "SYSDATETIME()");
    }

    public static void AddUpdateColumns(
        HashSet<string> tableColumns,
        List<string> setList,
        DynamicParameters parameters,
        string account)
    {
        AddUpdateColumn(tableColumns, setList, parameters, EditUser, "AuditEditUser", account);
        AddUpdateColumn(tableColumns, setList, parameters, EditTime, null, null, "SYSDATETIME()");
    }

    private static void AddInsertColumn(
        HashSet<string> tableColumns,
        List<string> columns,
        List<string> values,
        IDictionary<string, object> parameters,
        string columnName,
        string? paramName,
        object? paramValue,
        string? sqlValue = null)
    {
        if (!tableColumns.Contains(columnName) || ContainsColumn(columns, columnName))
            return;

        columns.Add($"[{columnName}]");
        if (sqlValue is not null)
        {
            values.Add(sqlValue);
            return;
        }

        values.Add($"@{paramName}");
        parameters[paramName!] = paramValue!;
    }

    private static void AddInsertColumn(
        HashSet<string> tableColumns,
        List<string> columns,
        List<string> values,
        DynamicParameters parameters,
        string columnName,
        string? paramName,
        object? paramValue,
        string? sqlValue = null)
    {
        if (!tableColumns.Contains(columnName) || ContainsColumn(columns, columnName))
            return;

        columns.Add($"[{columnName}]");
        if (sqlValue is not null)
        {
            values.Add(sqlValue);
            return;
        }

        values.Add($"@{paramName}");
        parameters.Add(paramName!, paramValue);
    }

    private static void AddUpdateColumn(
        HashSet<string> tableColumns,
        List<string> setList,
        IDictionary<string, object> parameters,
        string columnName,
        string? paramName,
        object? paramValue,
        string? sqlValue = null)
    {
        if (!tableColumns.Contains(columnName) || ContainsAssignment(setList, columnName))
            return;

        if (sqlValue is not null)
        {
            setList.Add($"[{columnName}] = {sqlValue}");
            return;
        }

        setList.Add($"[{columnName}] = @{paramName}");
        parameters[paramName!] = paramValue!;
    }

    private static void AddUpdateColumn(
        HashSet<string> tableColumns,
        List<string> setList,
        DynamicParameters parameters,
        string columnName,
        string? paramName,
        object? paramValue,
        string? sqlValue = null)
    {
        if (!tableColumns.Contains(columnName) || ContainsAssignment(setList, columnName))
            return;

        if (sqlValue is not null)
        {
            setList.Add($"[{columnName}] = {sqlValue}");
            return;
        }

        setList.Add($"[{columnName}] = @{paramName}");
        parameters.Add(paramName!, paramValue);
    }

    private static bool ContainsColumn(IEnumerable<string> columns, string columnName)
    {
        var token = $"[{columnName}]";
        return columns.Any(column => string.Equals(column, token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAssignment(IEnumerable<string> setList, string columnName)
    {
        var token = $"[{columnName}]";
        return setList.Any(set => set.TrimStart().StartsWith(token, StringComparison.OrdinalIgnoreCase));
    }
}
