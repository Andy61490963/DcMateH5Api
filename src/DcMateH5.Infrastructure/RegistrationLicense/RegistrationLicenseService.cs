using System.Security.Cryptography;
using System.Text;
using DcMateH5.Abstractions.RegistrationLicense;
using DcMateH5.Abstractions.RegistrationLicense.Model;

namespace DcMateH5.Infrastructure.RegistrationLicense;

/// <summary>
/// 授權碼解析服務
/// 
/// 保留舊版邏輯：
/// 1. 移除前 8 碼 Prefix
/// 2. Base64 解碼
/// 3. 使用固定 DES Key / IV 解密
/// 4. 以 '|' 分隔資料
/// 5. 預期共 5 個欄位
/// </summary>
public sealed class RegistrationLicenseService : IRegistrationLicenseService
{
    private const char SplitChar = '|';
    private const int PrefixLength = 8;
    private const int ExpectedFieldCount = 5;
    private const string DesKey = "Weyu0401";
    private const string DesIv = "54226552";

    private const string SuccessMessage = "Decrypt Success!!";
    private const string FormatErrorMessage = "Format Error";

    /// <summary>
    /// 解析授權碼
    /// </summary>
    /// <param name="licenseKey"></param>
    /// <param name="checkCode"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public LicenseParseResponse Parse(string licenseKey, string checkCode)
    {
        if (licenseKey is null)
        {
            throw new ArgumentNullException(nameof(licenseKey));
        }
        
        if (checkCode is null)
        {
            throw new ArgumentNullException(nameof(checkCode));
        }

        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return CreateFailResponse("LicenseKey is required.");
        }

        try
        {
            string decryptedText = Decrypt(licenseKey);
            string[] fields = decryptedText.Split(SplitChar);

            if (fields.Length != ExpectedFieldCount)
            {
                return CreateFailResponse(FormatErrorMessage);
            }

            return new LicenseParseResponse
            {
                CreateTime = fields[0],
                DbDataSource = fields[1],
                ExpiredDate = fields[2],
                NumOfReg = fields[3],
                CustomerName = fields[4],
                VerifyResult = true,
                ResultMessage = SuccessMessage
            };
        }
        catch (Exception ex)
        {
            return CreateFailResponse(ex.Message.Trim());
        }
    }

    private static LicenseParseResponse CreateFailResponse(string message)
    {
        return new LicenseParseResponse
        {
            CreateTime = string.Empty,
            DbDataSource = string.Empty,
            ExpiredDate = string.Empty,
            NumOfReg = string.Empty,
            CustomerName = string.Empty,
            VerifyResult = false,
            ResultMessage = message
        };
    }

    private static string Decrypt(string licenseKey)
    {
        string encryptedText = RemovePrefix(licenseKey);
        byte[] cipherBytes = Convert.FromBase64String(encryptedText);

#pragma warning disable SYSLIB0021
        using DESCryptoServiceProvider desProvider = new DESCryptoServiceProvider();
#pragma warning restore SYSLIB0021

        using MemoryStream memoryStream = new MemoryStream();
        using CryptoStream cryptoStream = new CryptoStream(
            memoryStream,
            desProvider.CreateDecryptor(
                Encoding.UTF8.GetBytes(DesKey),
                Encoding.UTF8.GetBytes(DesIv)),
            CryptoStreamMode.Write);

        cryptoStream.Write(cipherBytes, 0, cipherBytes.Length);
        cryptoStream.FlushFinalBlock();

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    private static string RemovePrefix(string licenseKey)
    {
        if (licenseKey.Length < PrefixLength)
        {
            throw new ArgumentException("LicenseKey length is invalid.");
        }

        return licenseKey.Substring(PrefixLength);
    }
}
