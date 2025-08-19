namespace DynamicForm.Helper;

/// <summary>
/// Swagger 文件的分類常數，避免重複字串與易於維護。
/// </summary>
public static class SwaggerGroups
{
    public const string ApiStats = nameof(ApiStats);
    public const string Security = nameof(Security);
    public const string Permission = nameof(Permission);
    public const string Enum = nameof(Enum);
    public const string Form = nameof(Form);
    
    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        { ApiStats, "所有 API 列表" },
        { Security, "登入、測試 API 權限" },
        { Permission, "群組、功能、權限設定" },
        { Enum, "列舉" },
        { Form, "主檔維護" }
    };
}
