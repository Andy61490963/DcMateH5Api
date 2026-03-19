namespace DcMateH5Api.MiddlewareExtension.Token;

public static class TokenRenewMiddlewareExtensions
{
    public static IApplicationBuilder UseTokenRenew(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TokenRenewMiddleware>();
    }
}