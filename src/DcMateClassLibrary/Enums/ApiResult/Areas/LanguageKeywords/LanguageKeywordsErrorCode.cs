using System.ComponentModel.DataAnnotations;

namespace ClassLibrary.Areas.LanguageKeywords;

public enum LanguageKeywordsErrorCode
{
    [Display(Name = "關鍵字已存在", Description = "關鍵字已存在")]
    KeyWordExisted,
}