using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum TableSchemaQueryType
{
    [Display(Name = "主表", Description = "主要更改的目標資料表(主檔)")]
    OnlyTable = 0,   
    
    [Display(Name = "明細表", Description = "主要更改的明細資料表(明細檔)")]
    OnlyDetail = 1,

    [Display(Name = "關聯表", Description = "多對多關聯使用的對應資料表")]
    OnlyMapping = 4,
    
    [Display(Name = "檢視表", Description = "呈現、搜尋特定條件的資料表(檢視表)")]
    OnlyView = 2,   
    
    [Display(Name = "主表與檢視表", Description = "兩種一次取出(前述兩者為共用資料表)")]
    All = 3      
}
