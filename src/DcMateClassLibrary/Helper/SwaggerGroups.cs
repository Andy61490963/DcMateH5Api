namespace DcMateClassLibrary.Helper;

/// <summary>
/// Swagger 分組名稱集中管理，避免字串分散與拼寫錯誤。
/// </summary>
public static class SwaggerGroups
{
    public const string ApiStatus = nameof(ApiStatus);
    public const string Enum = nameof(Enum);
    public const string Log = nameof(Log);

    public const string Form = nameof(Form);
    public const string FormWithMasterDetail = nameof(FormWithMasterDetail);
    public const string FormWithMultipleMapping = nameof(FormWithMultipleMapping);
    public const string FormTableValueFunction = nameof(FormTableValueFunction);
    public const string FormView = nameof(FormView);

    public const string Security = nameof(Security);
    public const string LanguageKeywords = nameof(LanguageKeywords);
    public const string Menu = nameof(Menu);

    public const string Wip = nameof(Wip);
    public const string Test = nameof(Test);

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        { ApiStatus, "Api 狀態" },
        { Enum, "列舉" },
        { Log, "系統紀錄" },

        { Form, "表單主檔維護" },
        { FormWithMasterDetail, "表單一對多維護" },
        { FormWithMultipleMapping, "表單多對多維護" },
        { FormTableValueFunction, "TVF 維護" },
        { FormView, "View 查詢維護" },

        { Security, "安全性 API" },
        { LanguageKeywords, "語系關鍵字" },
        { Menu, "選單功能" },

        { Wip, "在製" },
        { Test, "測試" }
    };
}
