using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum FormMaintenanceType
{
    /// <summary>
    /// 單純主檔維護，只維護一張表
    /// </summary>
    Master = 0,
    
    /// <summary>
    /// 主明細一對多維護
    /// </summary>
    MasterWithDetails = 1
}
