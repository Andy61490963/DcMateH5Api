using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum TableSchemaQueryType
{
    [Display(Name = "主表", Description = "主要更改的目標資料表(主檔)")]
    OnlyTable = 0,   
    
    [Display(Name = "檢視表", Description = "呈現、搜尋特定條件的資料表(檢視表)")]
    OnlyView = 1,   
    
    [Display(Name = "主表與檢視表", Description = "兩種一次取出(前述兩者為共用資料表)")]
    All = 2        
}