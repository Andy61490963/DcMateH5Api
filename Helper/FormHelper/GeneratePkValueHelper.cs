namespace DynamicForm.Helper;

public class GeneratePkValueHelper
{
    public static object GeneratePkValue(string pkType)
    {
        switch (pkType.ToLower())
        {
            case "uniqueidentifier": return Guid.NewGuid();
            case "decimal":
            case "numeric": return RandomHelper.GenerateRandomDecimal();
            
            case "bigint": return RandomHelper.NextSnowflakeId();
            case "int":    return unchecked((int)RandomHelper.NextSnowflakeId());
            
            case "nvarchar":
            case "varchar":
            case "char": return Guid.NewGuid().ToString("N");
            
            default: throw new NotSupportedException($"不支援的主鍵型別: {pkType}");
        }
    }
}