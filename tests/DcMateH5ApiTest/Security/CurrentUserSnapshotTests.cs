using DcMateClassLibrary.Models;
using Xunit;

namespace DcMateH5ApiTest.Security;

public class CurrentUserSnapshotTests
{
    [Fact]
    public void From_UnauthenticatedUser_UsesNotLoginUserAccount()
    {
        var user = CurrentUserSnapshot.From(null);

        Assert.False(user.IsAuthenticated);
        Assert.Equal(CurrentUserSnapshot.NotLoginUser, user.Account);
    }
}
