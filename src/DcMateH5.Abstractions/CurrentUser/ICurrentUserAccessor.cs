using DcMateClassLibrary.Models;

namespace DcMateH5.Abstractions.CurrentUser;

public interface ICurrentUserAccessor
{
    CurrentUserSnapshot Get();
}
