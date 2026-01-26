using DcMateH5Api.Controllers;

namespace DcMateH5Api.Services.CurrentUser.Interfaces;

public interface ICurrentUserAccessor
{
    CurrentUserSnapshot Get();
}
