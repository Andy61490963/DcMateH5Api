using System.ComponentModel.DataAnnotations;

namespace DcMateH5.Abstractions.RegistrationLicense.Model;

/// <summary>
/// 註冊碼產生請求
/// </summary>
public sealed class LicenseGenerateRequest
{
    /// <summary>
    /// 連線字串；只會擷取 Data Source，不會寫入帳密到註冊碼
    /// </summary>
    [Required]
    public required string ConnectionString { get; init; }

    /// <summary>
    /// 到期日
    /// </summary>
    public DateTime ExpiredDate { get; init; }

    /// <summary>
    /// 註冊數量
    /// </summary>
    [Range(1, int.MaxValue)]
    public int NumOfReg { get; init; }

    /// <summary>
    /// 客戶名稱
    /// </summary>
    [Required]
    public required string CustomerName { get; init; }
}
