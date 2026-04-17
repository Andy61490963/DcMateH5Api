using System.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DcMateH5.Abstractions.RegistrationLicense;
using DcMateH5.Abstractions.RegistrationLicense.Model;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

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
/// 6. 驗證授權中的 DbDataSource 是否與系統 ConnectionString 的 DataSource 一致
/// </summary>
public sealed class RegistrationLicenseService : IRegistrationLicenseService
{
    private static class LicenseConstants
    {
        public const char SplitChar = '|';
        public const int PrefixLength = 8;
        public const int ExpectedFieldCount = 5;

        public const string DesKey = "Weyu0401";
        public const string DesIv = "54226552";

        public const string SuccessMessage = "Decrypt Success!!";
        public const string FormatErrorMessage = "Format Error";
        public const string EmptyLicenseMessage = "LicenseKey is required.";
        public const string EmptyConnectionStringMessage = "Connection string is required.";
        public const string InvalidConnectionStringMessage = "Connection string is invalid.";
        public const string EmptyDataSourceMessage = "ConnectionString is required.";
        public const string DatabaseSourceMismatchMessage = "License database source does not match current application database source.";

        public const string ConnectionStringKey = "ConnectionStrings:Connection";
        public const string TcpPrefix = "tcp:";
        public const string DefaultSqlInstance = "MSSQLSERVER";
        public const int RandomPrefixByteCount = 4;
    }

    private readonly IConfiguration _configuration;

    public RegistrationLicenseService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// 產生註冊碼
    /// </summary>
    /// <param name="request">註冊碼產生請求</param>
    /// <returns>註冊碼產生結果</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public LicenseGenerateResponse Generate(LicenseGenerateRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        string createTime = DateTime.Now
            .ToString("yyyy-MM-dd HH:mm.fff", CultureInfo.InvariantCulture);
        string dbDataSource = ResolveGenerateDataSource(request);
        string expiredDate = ValidateAndFormatExpiredDate(request.ExpiredDate);
        string numOfReg = ValidateAndFormatNumOfReg(request.NumOfReg);
        string customerName = ValidateRequiredText(request.CustomerName, nameof(request.CustomerName));

        EnsureNoSplitChar(createTime, nameof(createTime));
        EnsureNoSplitChar(dbDataSource, nameof(request.ConnectionString));
        EnsureNoSplitChar(expiredDate, nameof(request.ExpiredDate));
        EnsureNoSplitChar(numOfReg, nameof(request.NumOfReg));
        EnsureNoSplitChar(customerName, nameof(request.CustomerName));

        string raw = string.Join(
            LicenseConstants.SplitChar,
            createTime,
            dbDataSource,
            expiredDate,
            numOfReg,
            customerName);

        string licenseKey = GenerateHexPrefix() + Encrypt(raw);

        return new LicenseGenerateResponse
        {
            LicenseKey = licenseKey,
            CheckCode = GenerateCheckCode(),
            CreateTime = createTime,
            DbDataSource = dbDataSource,
            ExpiredDate = expiredDate,
            NumOfReg = numOfReg,
            CustomerName = customerName
        };
    }

    /// <summary>
    /// 解析授權碼
    /// </summary>
    /// <param name="licenseKey">授權碼</param>
    /// <param name="checkCode">檢查碼</param>
    /// <returns>解析結果</returns>
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
            return CreateFailResponse(LicenseConstants.EmptyLicenseMessage);
        }

        try
        {
            string decryptedText = Decrypt(licenseKey);
            string[] fields = decryptedText.Split(LicenseConstants.SplitChar);

            if (fields.Length != LicenseConstants.ExpectedFieldCount)
            {
                return CreateFailResponse(LicenseConstants.FormatErrorMessage);
            }

            LicenseParseResponse response = new LicenseParseResponse
            {
                CreateTime = fields[0],
                DbDataSource = fields[1],
                ExpiredDate = fields[2],
                NumOfReg = fields[3],
                CustomerName = fields[4],
                VerifyResult = true,
                ResultMessage = LicenseConstants.SuccessMessage
            };

            return ValidateDatabaseSource(response);
        }
        catch (Exception ex)
        {
            return CreateFailResponse(ex.Message.Trim());
        }
    }

    private LicenseParseResponse ValidateDatabaseSource(LicenseParseResponse response)
    {
        string? connectionString = _configuration[LicenseConstants.ConnectionStringKey];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return CreateFailResponse(LicenseConstants.EmptyConnectionStringMessage);
        }

        string appDataSource;
        try
        {
            appDataSource = GetApplicationDataSource(connectionString);
        }
        catch (Exception)
        {
            return CreateFailResponse(LicenseConstants.InvalidConnectionStringMessage);
        }

        string normalizedLicenseDataSource = NormalizeLicenseDataSource(response.DbDataSource);
        string normalizedAppDataSource = NormalizeApplicationDataSource(appDataSource);

        if (!string.Equals(
                normalizedLicenseDataSource,
                normalizedAppDataSource,
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateFailResponse(LicenseConstants.DatabaseSourceMismatchMessage);
        }

        return response;
    }

    private static string GetApplicationDataSource(string connectionString)
    {
        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException(LicenseConstants.InvalidConnectionStringMessage);
        }

        return builder.DataSource;
    }

    private static string ResolveGenerateDataSource(LicenseGenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionString))
        {
            throw new ArgumentException(LicenseConstants.EmptyDataSourceMessage, nameof(request.ConnectionString));
        }

        string dataSource;
        try
        {
                dataSource = FormatLicenseDataSource(GetApplicationDataSource(request.ConnectionString));
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                LicenseConstants.InvalidConnectionStringMessage,
                nameof(request.ConnectionString),
                ex);
        }

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            throw new ArgumentException(LicenseConstants.EmptyDataSourceMessage, nameof(request.ConnectionString));
        }

        return dataSource;
    }

    private static string ValidateAndFormatExpiredDate(DateTime expiredDate)
    {
        if (expiredDate == default)
        {
            throw new ArgumentException("ExpiredDate is required.", nameof(expiredDate));
        }

        return expiredDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    private static string ValidateAndFormatNumOfReg(int numOfReg)
    {
        if (numOfReg <= 0)
        {
            throw new ArgumentException("NumOfReg must be greater than 0.", nameof(numOfReg));
        }

        return numOfReg.ToString(CultureInfo.InvariantCulture);
    }

    private static string ValidateRequiredText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }

    private static void EnsureNoSplitChar(string value, string parameterName)
    {
        if (value.Contains(LicenseConstants.SplitChar))
        {
            throw new ArgumentException(
                $"{parameterName} cannot contain '{LicenseConstants.SplitChar}'.",
                parameterName);
        }
    }

    private static string FormatLicenseDataSource(string appDataSource)
    {
        string normalized = NormalizeApplicationDataSource(appDataSource);

        string hostPart = normalized;
        string? instancePart = null;
        string? portPart = null;

        int slashIndex = normalized.IndexOf('\\');
        if (slashIndex >= 0)
        {
            hostPart = normalized.Substring(0, slashIndex);

            string instanceAndPort = normalized[(slashIndex + 1)..];
            int commaIndex = instanceAndPort.IndexOf(',');
            if (commaIndex >= 0)
            {
                instancePart = instanceAndPort.Substring(0, commaIndex);
                portPart = instanceAndPort[(commaIndex + 1)..];
            }
            else
            {
                instancePart = instanceAndPort;
            }
        }
        else
        {
            int commaIndex = normalized.IndexOf(',');
            if (commaIndex >= 0)
            {
                hostPart = normalized.Substring(0, commaIndex);
                portPart = normalized[(commaIndex + 1)..];
            }
        }

        instancePart = string.IsNullOrWhiteSpace(instancePart)
            ? LicenseConstants.DefaultSqlInstance
            : instancePart.Trim();

        return $"{LicenseConstants.TcpPrefix}{BuildNormalizedDataSource(hostPart.Trim(), instancePart, portPart?.Trim())}";
    }

    /// <summary>
    /// 將授權內容中的資料來源正規化成：
    /// host[\instance][,port]
    /// 
    /// 範例：
    /// tcp:10.0.20.20\MSSQLSERVER,1433 -> 10.0.20.20,1433
    /// tcp:10.0.20.20\SQLEXPRESS,1433 -> 10.0.20.20\SQLEXPRESS,1433
    /// </summary>
    private static string NormalizeLicenseDataSource(string? licenseDataSource)
    {
        if (string.IsNullOrWhiteSpace(licenseDataSource))
        {
            return string.Empty;
        }

        string value = licenseDataSource.Trim();

        if (value.StartsWith(LicenseConstants.TcpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(LicenseConstants.TcpPrefix.Length);
        }

        string hostPart = value;
        string? instancePart = null;
        string? portPart = null;

        int slashIndex = value.IndexOf('\\');
        if (slashIndex >= 0)
        {
            hostPart = value.Substring(0, slashIndex);

            string instanceAndPort = value[(slashIndex + 1)..];
            int commaIndex = instanceAndPort.IndexOf(',');

            if (commaIndex >= 0)
            {
                instancePart = instanceAndPort.Substring(0, commaIndex);
                portPart = instanceAndPort[(commaIndex + 1)..];
            }
            else
            {
                instancePart = instanceAndPort;
            }
        }
        else
        {
            int commaIndex = value.IndexOf(',');
            if (commaIndex >= 0)
            {
                hostPart = value.Substring(0, commaIndex);
                portPart = value[(commaIndex + 1)..];
            }
        }

        hostPart = hostPart.Trim();
        instancePart = string.IsNullOrWhiteSpace(instancePart) ? null : instancePart.Trim();
        portPart = string.IsNullOrWhiteSpace(portPart) ? null : portPart.Trim();

        if (string.Equals(instancePart, LicenseConstants.DefaultSqlInstance, StringComparison.OrdinalIgnoreCase))
        {
            instancePart = null;
        }

        return BuildNormalizedDataSource(hostPart, instancePart, portPart);
    }

    /// <summary>
    /// 將 appsettings ConnectionString 內的 DataSource 正規化成：
    /// host[\instance][,port]
    /// 
    /// 範例：
    /// 10.0.20.20,1433 -> 10.0.20.20,1433
    /// 10.0.20.20\SQLEXPRESS -> 10.0.20.20\SQLEXPRESS
    /// </summary>
    private static string NormalizeApplicationDataSource(string? appDataSource)
    {
        if (string.IsNullOrWhiteSpace(appDataSource))
        {
            return string.Empty;
        }

        string value = appDataSource.Trim();

        string hostPart = value;
        string? instancePart = null;
        string? portPart = null;

        int slashIndex = value.IndexOf('\\');
        if (slashIndex >= 0)
        {
            hostPart = value.Substring(0, slashIndex);

            string instanceAndPort = value[(slashIndex + 1)..];
            int commaIndex = instanceAndPort.IndexOf(',');

            if (commaIndex >= 0)
            {
                instancePart = instanceAndPort.Substring(0, commaIndex);
                portPart = instanceAndPort[(commaIndex + 1)..];
            }
            else
            {
                instancePart = instanceAndPort;
            }
        }
        else
        {
            int commaIndex = value.IndexOf(',');
            if (commaIndex >= 0)
            {
                hostPart = value.Substring(0, commaIndex);
                portPart = value[(commaIndex + 1)..];
            }
        }

        hostPart = hostPart.Trim();
        instancePart = string.IsNullOrWhiteSpace(instancePart) ? null : instancePart.Trim();
        portPart = string.IsNullOrWhiteSpace(portPart) ? null : portPart.Trim();

        if (string.Equals(instancePart, LicenseConstants.DefaultSqlInstance, StringComparison.OrdinalIgnoreCase))
        {
            instancePart = null;
        }

        return BuildNormalizedDataSource(hostPart, instancePart, portPart);
    }

    private static string BuildNormalizedDataSource(string hostPart, string? instancePart, string? portPart)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(hostPart);

        if (!string.IsNullOrWhiteSpace(instancePart))
        {
            builder.Append('\\');
            builder.Append(instancePart);
        }

        if (!string.IsNullOrWhiteSpace(portPart))
        {
            builder.Append(',');
            builder.Append(portPart);
        }

        return builder.ToString();
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
                Encoding.UTF8.GetBytes(LicenseConstants.DesKey),
                Encoding.UTF8.GetBytes(LicenseConstants.DesIv)),
            CryptoStreamMode.Write);

        cryptoStream.Write(cipherBytes, 0, cipherBytes.Length);
        cryptoStream.FlushFinalBlock();

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    private static string Encrypt(string raw)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(raw);

        using MemoryStream memoryStream = new MemoryStream();
        using DES des = DES.Create();
        using CryptoStream cryptoStream = new CryptoStream(
            memoryStream,
            des.CreateEncryptor(
                Encoding.UTF8.GetBytes(LicenseConstants.DesKey),
                Encoding.UTF8.GetBytes(LicenseConstants.DesIv)),
            CryptoStreamMode.Write);

        cryptoStream.Write(buffer, 0, buffer.Length);
        cryptoStream.FlushFinalBlock();

        return Convert.ToBase64String(memoryStream.ToArray());
    }

    private static string GenerateCheckCode()
    {
        return GenerateHexPrefix() + Encrypt(GenerateHexPrefix());
    }

    private static string GenerateHexPrefix()
    {
        byte[] buffer = RandomNumberGenerator.GetBytes(LicenseConstants.RandomPrefixByteCount);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }

    private static string RemovePrefix(string licenseKey)
    {
        if (licenseKey.Length < LicenseConstants.PrefixLength)
        {
            throw new ArgumentException("LicenseKey length is invalid.");
        }

        return licenseKey.Substring(LicenseConstants.PrefixLength);
    }
}
