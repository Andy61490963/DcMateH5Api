using DcMateH5.Abstractions.Token;
using DcMateH5.Abstractions.Token.Model;

namespace DcMateH5Api.MiddlewareExtension.Token;

public class TokenRenewMiddleware
{
    private static class HeaderNames
    {
        public const string Authorization = "Authorization";
        public const string BearerPrefix = "Bearer ";
        public const string TokenExpire = "X-Token-Expire";
    }

    private readonly RequestDelegate _next;
    private readonly ITokenService _tokenService;

    public TokenRenewMiddleware(
        RequestDelegate next,
        ITokenService tokenService)
    {
        _next = next;
        _tokenService = tokenService;
    }

    public async Task Invoke(HttpContext context)
    {
        string currentToken = ExtractBearerToken(context.Request);

        context.Response.OnStarting(() =>
        {
            TryRenewToken(context, currentToken);
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private void TryRenewToken(HttpContext context, string currentToken)
    {
        if (string.IsNullOrWhiteSpace(currentToken))
        {
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300)
        {
            return;
        }

        TokenValidationResult validationResult = _tokenService.ValidateToken(currentToken);

        if (!validationResult.IsValid)
        {
            return;
        }

        bool shouldRenew = ShouldRenew(validationResult);

        if (!shouldRenew)
        {
            return;
        }

        TokenPayload renewPayload = new TokenPayload
        {
            Domain = validationResult.Domain,
            TokenMinutes = validationResult.TokenMinutes,
            TokenSeq = validationResult.TokenSeq,
            UserId = validationResult.UserId,
            Account = validationResult.Account,
            SessionId = validationResult.SessionId,
            UserLv = validationResult.UserLv
        };

        GenerateTokenResult newToken = _tokenService.GenerateToken(renewPayload);

        // ★ MODIFY: 不另外用 X-Renew-Token，直接沿用舊的 X-Auth-Token
        context.Response.Headers[HeaderNames.Authorization] =
            $"{HeaderNames.BearerPrefix}{newToken.TokenKey}";
        
        DateTime expireTime = new DateTime(validationResult.ExpireTicks, DateTimeKind.Local);

        context.Response.Headers[HeaderNames.TokenExpire] =
            expireTime.ToString("o"); // ISO 8601
    }

    private static bool ShouldRenew(TokenValidationResult validationResult)
    {
        // 方案 1：每次都 renew
        // return true;

        // 方案 2：快過期才 renew（建議）
        // DateTime expireTimeUtc = new DateTime(validationResult.ExpireTicks, DateTimeKind.Utc);
        // TimeSpan remaining = expireTimeUtc - DateTime.UtcNow;
        //
        // return remaining <= TimeSpan.FromMinutes(5);

        // 老闆要求要一直回新 token，這邊一定會有效能問題
        return true;
    }

    private static string ExtractBearerToken(HttpRequest request)
    {
        string authorizationHeader = request.Headers[HeaderNames.Authorization].FirstOrDefault() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            authorizationHeader = request.Headers[HeaderNames.Authorization].FirstOrDefault() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return string.Empty;
        }

        if (!authorizationHeader.StartsWith(HeaderNames.BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return authorizationHeader[HeaderNames.BearerPrefix.Length..].Trim();
    }
}