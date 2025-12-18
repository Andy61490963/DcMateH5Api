using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum FormFunctionType
{
    [Display(Name = "主檔維護", Description = "主檔維護")]
    MasterMaintenance = 0,

    [Display(Name = "一對多維護", Description = "一對多維護")]
    MasterDetailMaintenance = 1,

    [Display(Name = "多對多維護", Description = "多對多維護")]
    MultipleMappingMaintenance = 2
}
