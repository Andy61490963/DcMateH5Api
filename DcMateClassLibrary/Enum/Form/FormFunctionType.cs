using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum FormFunctionType
{
    /// <summary>
    /// 主檔維護
    ///
    /// 特性：
    /// - 單一資料表
    /// - 無明細表
    /// </summary>
    [Display(Name = "主檔維護", Description = "主檔維護")]
    MasterMaintenance = 0,

    /// <summary>
    /// 一對多維護
    ///
    /// 特性：
    /// - 一筆主檔
    /// - 對應多筆明細
    /// - 主從關聯（Master / Detail）
    /// </summary>
    [Display(Name = "一對多維護", Description = "一對多維護")]
    MasterDetailMaintenance = 1,

    /// <summary>
    /// 多對多維護
    ///
    /// 特性：
    /// - 多筆主資料互相關聯
    /// - 中介關聯表（Mapping Table）
    /// </summary>
    [Display(Name = "多對多維護", Description = "多對多維護")]
    MultipleMappingMaintenance = 2,
    
    /// <summary>
    /// TVF 維護
    ///
    /// 特性：
    /// TVF
    /// </summary>
    [Display(Name = "TVF 維護", Description = "TVF 維護")]
    TableValueFunctionMaintenance = 3
}
