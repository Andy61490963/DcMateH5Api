using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum TableStatusType
{
    [Display(Name = "編輯中", Description = "沒有經過儲存的表，經設定但尚未啟用")]
    Draft = 0,   
    
    [Display(Name = "啟用", Description = "經過儲存的表，啟用之主檔維護")]
    Active = 1,  
    
    [Display(Name = "停用", Description = "停用之主檔維護")]
    Disabled = 2 
}