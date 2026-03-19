using DcMateH5.Abstractions.Token.Model;

namespace DcMateH5.Abstractions.Token;

public interface ITokenService
{
    /// <summary>
    /// 產生 Token
    /// </summary>
    GenerateTokenResult GenerateToken(TokenPayload payload);

    /// <summary>
    /// 續期 Token
    /// </summary>
    RenewTokenResult RenewToken(string oldToken);

    /// <summary>
    /// 驗證 Token
    /// </summary>
    TokenValidationResult ValidateToken(string token);
}