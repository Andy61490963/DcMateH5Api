using DcMateH5.Abstractions.RegistrationLicense.Model;

namespace DcMateH5.Abstractions.RegistrationLicense;

public interface IRegistrationLicenseService
{
    /// <summary>
    /// 解析授權碼
    /// </summary>
    /// <param name="licenseKey"></param>
    /// <param name="checkCode"></param>
    /// <returns></returns>
    LicenseParseResponse Parse(string licenseKey, string checkCode);
}