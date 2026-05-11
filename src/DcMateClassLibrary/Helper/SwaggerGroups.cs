namespace DcMateClassLibrary.Helper;

/// <summary>
/// Swagger 文件分組名稱集中管理，避免 controller 與 Program.cs 使用不同字串。
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
    public const string Eqm = nameof(Eqm);
    public const string Mms = nameof(Mms);
    public const string Test = nameof(Test);

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        { ApiStatus, "API 狀態" },
        { Enum, "列舉" },
        { Log, "系統紀錄" },

        { Form, "表單設計" },
        { FormWithMasterDetail, "主從表單" },
        { FormWithMultipleMapping, "多重對應表單" },
        { FormTableValueFunction, "TVF 表單" },
        { FormView, "View 表單" },

        { Security, "安全性 API" },
        { LanguageKeywords, "多語系關鍵字" },
        { Menu, "選單" },

        { Wip, "WIP" },
        { Eqm, "EQM" },
        { Mms, "MMS" },
        { Test, "測試" }
    };
}
