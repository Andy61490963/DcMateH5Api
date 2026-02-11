using DcMateClassLibrary.Models;
using DcMateH5.Abstractions.CurrentUser;

namespace DcMateH5Api.Services.CurrentUser;

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
