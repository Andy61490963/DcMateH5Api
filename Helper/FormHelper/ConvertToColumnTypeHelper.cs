namespace DynamicForm.Helper;

public static class ConvertToColumnTypeHelper
{
    /// <summary>
    /// 型別轉換，集中管理。可以支援 int、datetime、decimal、bool、string、null…
    /// </summary>
    public static object? Convert(string? sqlType, object? value)
    {
        if (value is null) return null;
        var str = value.ToString();

        if (string.IsNullOrWhiteSpace(sqlType)) return value;

        switch (sqlType.ToLower())
        {
            case "int":
            case "bigint":
                return long.TryParse(str, out var l) ? l : null;

            case "decimal":
            case "numeric":
                return decimal.TryParse(str, out var d) ? d : null;

            case "bit":
                return str == "1" || str.Equals("true", StringComparison.OrdinalIgnoreCase);

            case "datetime":
            case "smalldatetime":
            case "date":
                return DateTime.TryParse(str, out var dt) ? dt : null;

            case "nvarchar":
            case "varchar":
            case "nchar":
            case "char":
            case "text":
                return str;

            default:
                return str;
        }
    }
    
    /// <summary>
    /// 根據 SQL 型別，將傳入 id 轉換為 DB 支援的型別
    /// </summary>
    public static object ConvertPkType(string? id, string pkType)
    {
        if (id == null) throw new ArgumentNullException(nameof(id));
        switch (pkType.ToLower())
        {
            case "uniqueidentifier": return Guid.Parse(id);
            
            case "decimal":
            case "numeric": return System.Convert.ToDecimal(id);
            
            case "bigint": return System.Convert.ToInt64(id);
            case "int": return System.Convert.ToInt32(id);
            
            case "nvarchar":
            case "varchar":
            case "char": return id;
            default: throw new NotSupportedException($"不支援的型別: {pkType}");
        }
    }
}