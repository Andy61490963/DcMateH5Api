using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum TableQueryType
{
    [Display(Name = "都查", Description = "都查")]
    All = 0,   
    
    [Display(Name = "查詢主表", Description = "查詢主表")]
    QueryTable = 1,
    
    [Display(Name = "查詢View表", Description = "查詢View表")]
    OnlyViewTable = 2,
    
    [Display(Name = "查詢 Table-Valued Function 表", Description = "查詢 Table-Valued Function 表")]
    OnlyFunction = 3
}
