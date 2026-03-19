namespace DcMateH5.Abstractions.RegistrationLicense.Model;

/// <summary>
/// 授權碼解析結果
/// </summary>
public sealed class LicenseParseResponse
{
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

    /// <summary>
    /// 是否驗證成功
    /// </summary>
    public bool VerifyResult { get; init; }

    /// <summary>
    /// 結果訊息
    /// </summary>
    public string ResultMessage { get; init; } = string.Empty;
}