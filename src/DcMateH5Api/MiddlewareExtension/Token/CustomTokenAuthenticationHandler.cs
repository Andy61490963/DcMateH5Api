using System.Security.Claims;
using System.Text.Encodings.Web;
using DcMateH5.Abstractions.Token;
using DcMateH5.Abstractions.Token.Model;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DcMateH5Api.MiddlewareExtension.Token;

public class CustomTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private static class HeaderNames
    {
        // 避免 magic string
        public const string Authorization = "Authorization";
        public const string BearerPrefix = "Bearer ";
    }

    private readonly ITokenService _tokenService;

    public CustomTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenService tokenService)
        : base(options, logger, encoder)
    {
        _tokenService = tokenService;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string token = ExtractBearerToken(Request);

        // 沒帶 token，不算系統錯，交給後續 [Authorize] 判斷
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        TokenValidationResult result = _tokenService.ValidateToken(token);

        if (!result.IsValid)
        {
            return Task.FromResult(AuthenticateResult.Fail(result.Message));
        }

        List<Claim> claims = new List<Claim>
        {
            new (AppClaimTypes.Account, result.Account),
            new (AppClaimTypes.UserId, result.UserId.ToString()),
            new (AppClaimTypes.UserLv, result.UserLv),
            new (TokenClaimTypes.SessionId, result.SessionId),
            new (TokenClaimTypes.TokenSeq, result.TokenSeq.ToString())
        };

        ClaimsIdentity identity = new ClaimsIdentity(
            claims,
            CustomTokenAuthenticationDefaults.AuthenticationScheme);

        ClaimsPrincipal principal = new ClaimsPrincipal(identity);

        AuthenticationTicket ticket = new AuthenticationTicket(
            principal,
            CustomTokenAuthenticationDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // 未登入或 token 無效時，統一回 401
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json; charset=utf-8";

        string json = """
        {
          "isSuccess": false,
          "data": null,
          "code": "Unauthorized",
          "message": "尚未登入或 Token 無效"
        }
        """;

        return Response.WriteAsync(json);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        // 已登入但沒權限時，統一回 403
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json; charset=utf-8";

        string json = """
        {
          "isSuccess": false,
          "data": null,
          "code": "Forbidden",
          "message": "沒有權限執行此操作"
        }
        """;

        return Response.WriteAsync(json);
    }

    private static string ExtractBearerToken(HttpRequest request)
    {
        string authorizationHeader = request.Headers[HeaderNames.Authorization].FirstOrDefault() ?? string.Empty;

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
    
    public static class CustomTokenAuthenticationDefaults
    {
        public const string AuthenticationScheme = "WeYuToken";
    }
}