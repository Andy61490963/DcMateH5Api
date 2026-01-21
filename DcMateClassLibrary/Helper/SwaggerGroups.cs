namespace DcMateH5Api.Helper;

/// <summary>
/// Swagger 文件的分類，避免重複字串與易於維護。
/// </summary>
public static class SwaggerGroups
{
    public const string ApiStatus = nameof(ApiStatus);
    public const string Security = nameof(Security);
    public const string Permission = nameof(Permission);
    public const string Enum = nameof(Enum);
    public const string Form = nameof(Form);
    public const string FormWithMasterDetail = nameof(FormWithMasterDetail);
    public const string FormWithMultipleMapping = nameof(FormWithMultipleMapping);
    public const string Log = nameof(Log);
    
    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        { ApiStatus, "Api 狀態" },
        { Security, "登入、測試 API 權限" },
        { Permission, "群組、功能、權限設定" },
        { Enum, "列舉" },
        { Form, "主檔維護" },
        { FormWithMasterDetail, "主明細維護" },
        { FormWithMultipleMapping, "多對多維護" },
        { Log, "系統紀錄" },
    };
}
