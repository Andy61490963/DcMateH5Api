using DcMateH5Api.Controllers;
using DcMateH5Api.Services.CurrentUser.Interfaces;

namespace DcMateH5Api.Services.CurrentUser.Services;

public class HttpCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserAccessor(IHttpContextAccessor accessor)
    {
        _httpContextAccessor = accessor;
    }

    public CurrentUserSnapshot Get()
    {
        var ctx = _httpContextAccessor.HttpContext;

        return CurrentUserSnapshot.From(ctx!.User);
    }
}
