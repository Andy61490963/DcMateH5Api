namespace DcMateH5.Abstractions.RegistrationLicense.Model;

/// <summary>
/// 註冊碼產生結果
/// </summary>
public sealed class LicenseGenerateResponse
{
    /// <summary>
    /// 註冊碼
    /// </summary>
    public string LicenseKey { get; init; } = string.Empty;

    /// <summary>
    /// 檢查碼
    /// </summary>
    public string CheckCode { get; init; } = string.Empty;

    /// <summary>
    /// 建立時間
    /// </summary>
    public string CreateTime { get; init; } = string.Empty;

    /// <summary>
    /// 資料庫來源
    /// </summary>
    public string DbDataSource { get; init; } = string.Empty;

    /// <summary>
    /// 到期日
    /// </summary>
    public string ExpiredDate { get; init; } = string.Empty;

    /// <summary>
    /// 註冊數量
    /// </summary>
    public string NumOfReg { get; init; } = string.Empty;

    /// <summary>
    /// 客戶名稱
    /// </summary>
    public string CustomerName { get; init; } = string.Empty;
}
