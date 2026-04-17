using DcMateH5.Abstractions.RegistrationLicense.Model;

namespace DcMateH5.Abstractions.RegistrationLicense;

public interface IRegistrationLicenseService
{
    /// <summary>
    /// 產生註冊碼
    /// </summary>
    /// <param name="request">註冊碼產生請求</param>
    /// <returns>註冊碼產生結果</returns>
    LicenseGenerateResponse Generate(LicenseGenerateRequest request);

    /// <summary>
    /// 解析授權碼
    /// </summary>
    /// <param name="licenseKey"></param>
    /// <param name="checkCode"></param>
    /// <returns></returns>
    LicenseParseResponse Parse(string licenseKey, string checkCode);
}
