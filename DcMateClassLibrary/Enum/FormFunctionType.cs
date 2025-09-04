using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum FormFunctionType
{
    [Display(Name = "主檔維護", Description = "主檔維護")]
    NotMasterDetail = 0,   
    
    [Display(Name = "主明細維護", Description = "主明細維護")]
    MasterDetail = 1,   
}