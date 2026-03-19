using System.Security.Cryptography;
using System.Text;
using DcMateH5.Abstractions.Token;
using DcMateH5.Abstractions.Token.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DcMateH5.Infrastructure.Token;

public class TokenService : ITokenService
{
    private static class ErrorMessages
    {
        public const string TokenEmpty = "Token is empty.";
        public const string TokenInvalid = "Token format is invalid.";
        public const string TokenDecryptFailed = "Token decrypt failed.";
        public const string TokenExpired = "Token expired.";
        public const string TokenValid = "Token is valid.";
        public const string RenewSuccess = "Token renewed successfully.";
    }

    private readonly ILogger<TokenService> _logger;
    private readonly TokenOptions _options;

    public TokenService(
        IOptions<TokenOptions> options,
        ILogger<TokenService> logger)
    {
        _options = options.Value;
        _logger = logger;

        ValidateOptions(_options);
    }

    /// <summary>
    /// 產生 Token
    /// </summary>
    /// <param name="payload">Token 內容</param>
    /// <returns>Token 產生結果</returns>
    public GenerateTokenResult GenerateToken(TokenPayload payload)
    {
        try
        {
            DateTime now = DateTime.Now; // 建議改 
            DateTime expireTime = now.AddMinutes(payload.TokenMinutes);

            TokenPayload finalPayload = payload with
            {
                ExpireTicks = expireTime.Ticks,
                IssuedTicks = now.Ticks,
                Domain = string.IsNullOrWhiteSpace(payload.Domain)
                    ? _options.Domain
                    : payload.Domain
            };

            string raw = BuildRawPayload(finalPayload);
            string tokenKey = Encrypt(raw);

            return new GenerateTokenResult
            {
                TokenKey = tokenKey,
                Expiration = expireTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateToken failed. Account: {Account}, UserId: {UserId}", payload.Account, payload.UserId);
            throw;
        }
    }

    /// <summary>
    /// 續期 Token
    /// </summary>
    /// <param name="oldToken">舊 Token</param>
    /// <returns>續期結果</returns>
    public RenewTokenResult RenewToken(string oldToken)
    {
        try
        {
            TokenValidationResult validationResult = ValidateToken(oldToken);

            if (!validationResult.IsValid)
            {
                return new RenewTokenResult
                {
                    IsSuccess = false,
                    Message = validationResult.Message,
                    AccountNo = validationResult.Account,
                    TokenKey = string.Empty,
                    ExpirationText = null,
                    TokenSeq = 0
                };
            }

            TokenPayload newPayload = new TokenPayload
            {
                Domain = validationResult.Domain,
                TokenMinutes = validationResult.TokenMinutes,
                TokenSeq = validationResult.TokenSeq,
                UserId = validationResult.UserId,
                Account = validationResult.Account,
                SessionId = validationResult.SessionId,
                UserLv = validationResult.UserLv
            };

            GenerateTokenResult newToken = GenerateToken(newPayload);

            return new RenewTokenResult
            {
                IsSuccess = true,
                Message = ErrorMessages.RenewSuccess,
                AccountNo = validationResult.Account,
                TokenKey = newToken.TokenKey,
                ExpirationText = newToken.Expiration,
                TokenSeq = validationResult.TokenSeq
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RenewToken failed.");
            throw;
        }
    }

    /// <summary>
    /// 驗證 Token 是否有效
    /// </summary>
    /// <param name="token">Token</param>
    /// <returns>驗證結果</returns>
    public TokenValidationResult ValidateToken(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Fail(ErrorMessages.TokenEmpty);
            }

            string decrypted = Decrypt(token);
            string[] parts = decrypted.Split('|');

            // 新版 payload 改成 9 欄
            if (parts.Length < 9)
            {
                return Fail(ErrorMessages.TokenInvalid);
            }

            bool isExpireTicksValid = long.TryParse(parts[0], out long expireTicks);
            string domain = parts[1];
            bool isTokenMinutesValid = int.TryParse(parts[2], out int tokenMinutes);
            bool isTokenSeqValid = int.TryParse(parts[3], out int tokenSeq);
            bool isUserIdValid = Guid.TryParse(parts[4], out Guid userId);
            string account = parts[5];
            string sessionId = parts[6];
            bool isIssuedTicksValid = long.TryParse(parts[7], out long issuedTicks);
            string userLv = parts[8]; // ★ NEW: 解析 UserLv

            if (!isExpireTicksValid ||
                !isTokenMinutesValid ||
                !isTokenSeqValid ||
                !isUserIdValid ||
                string.IsNullOrWhiteSpace(account) ||
                string.IsNullOrWhiteSpace(sessionId) ||
                !isIssuedTicksValid ||
                string.IsNullOrWhiteSpace(userLv)) // ★ NEW
            {
                return Fail(ErrorMessages.TokenInvalid);
            }

            long nowTicks = DateTime.Now.Ticks; // 建議改 

            if (nowTicks > expireTicks)
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Message = ErrorMessages.TokenExpired,
                    UserId = userId,
                    Account = account,
                    SessionId = sessionId,
                    TokenSeq = tokenSeq,
                    TokenMinutes = tokenMinutes,
                    Domain = domain,
                    ExpireTicks = expireTicks,
                    IssuedTicks = issuedTicks,
                    UserLv = userLv // ★ NEW
                };
            }

            return new TokenValidationResult
            {
                IsValid = true,
                Message = ErrorMessages.TokenValid,
                UserId = userId,
                Account = account,
                SessionId = sessionId,
                TokenSeq = tokenSeq,
                TokenMinutes = tokenMinutes,
                Domain = domain,
                ExpireTicks = expireTicks,
                IssuedTicks = issuedTicks,
                UserLv = userLv 
            };
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "ValidateToken decrypt failed.");
            return Fail(ErrorMessages.TokenDecryptFailed);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "ValidateToken base64 format invalid.");
            return Fail(ErrorMessages.TokenInvalid);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "ValidateToken token length invalid.");
            return Fail(ErrorMessages.TokenInvalid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ValidateToken failed.");
            throw;
        }
    }

    /// <summary>
    /// 建立失敗驗證結果
    /// </summary>
    private static TokenValidationResult Fail(string msg)
    {
        return new TokenValidationResult
        {
            IsValid = false,
            Message = msg
        };
    }

    /// <summary>
    /// 加密 Token 字串
    /// </summary>
    private string Encrypt(string raw)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(raw);

        using MemoryStream memoryStream = new MemoryStream();
        using DES des = DES.Create();
        using CryptoStream cryptoStream = new CryptoStream(
            memoryStream,
            des.CreateEncryptor(
                Encoding.UTF8.GetBytes(_options.RgbKey),
                Encoding.UTF8.GetBytes(_options.RgbIV)),
            CryptoStreamMode.Write);

        cryptoStream.Write(buffer, 0, buffer.Length);
        cryptoStream.FlushFinalBlock();

        string result = Convert.ToBase64String(memoryStream.ToArray());
        result = result.Split('=')[0];
        result = result.Replace('+', '-');
        result = result.Replace('/', '_');

        return _options.PrefixWord + result;
    }

    /// <summary>
    /// 解密 Token 字串
    /// </summary>
    private string Decrypt(string keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString))
        {
            throw new CryptographicException("Token is empty.");
        }

        if (!string.IsNullOrEmpty(_options.PrefixWord))
        {
            if (!keyString.StartsWith(_options.PrefixWord, StringComparison.Ordinal))
            {
                throw new CryptographicException("Token prefix is invalid.");
            }

            keyString = keyString[_options.PrefixWord.Length..];
        }

        keyString = keyString.Replace('-', '+');
        keyString = keyString.Replace('_', '/');

        switch (keyString.Length % 4)
        {
            case 0:
                break;
            case 2:
                keyString += "==";
                break;
            case 3:
                keyString += "=";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(keyString), "Illegal base64url string!");
        }

        byte[] buffer = Convert.FromBase64String(keyString);

        using MemoryStream memoryStream = new MemoryStream();
        using DES des = DES.Create();
        using CryptoStream cryptoStream = new CryptoStream(
            memoryStream,
            des.CreateDecryptor(
                Encoding.UTF8.GetBytes(_options.RgbKey),
                Encoding.UTF8.GetBytes(_options.RgbIV)),
            CryptoStreamMode.Write);

        cryptoStream.Write(buffer, 0, buffer.Length);
        cryptoStream.FlushFinalBlock();

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    private static void ValidateOptions(TokenOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RgbKey) || options.RgbKey.Length != 8)
        {
            throw new InvalidOperationException("TokenOptions.RgbKey 必須為 8 碼。");
        }

        if (string.IsNullOrWhiteSpace(options.RgbIV) || options.RgbIV.Length != 8)
        {
            throw new InvalidOperationException("TokenOptions.RgbIV 必須為 8 碼。");
        }

        if (options.DefaultTokenKeyMinutes <= 0)
        {
            throw new InvalidOperationException("TokenOptions.DefaultTokenKeyMinutes 必須大於 0。");
        }
    }

    private static string BuildRawPayload(TokenPayload payload)
    {
        // payload 改成 9 欄，最後補 UserLv
        return string.Join('|',
            payload.ExpireTicks,
            payload.Domain,
            payload.TokenMinutes,
            payload.TokenSeq,
            payload.UserId,
            payload.Account,
            payload.SessionId,
            payload.IssuedTicks,
            payload.UserLv); 
    }
}