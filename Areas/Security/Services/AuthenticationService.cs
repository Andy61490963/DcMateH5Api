using ClassLibrary;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.ViewModels;
using DcMateH5Api.SqlHelper;
using DcMateH5Api.Models;

namespace DcMateH5Api.Areas.Security.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;

    public AuthenticationService(
        SQLGenerateHelper sqlHelper,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator)
    {
        _sqlHelper = sqlHelper;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<LoginResponseViewModel>> AuthenticateAsync(string account, string password, CancellationToken ct = default)
    {
        var where = new WhereBuilder<UserAccount>()
            .AndEq(x => x.Account, account)
            .AndNotDeleted();

        var user = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);

        if (user == null)
        {
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, AuthenticationErrorCode.UserNotFound.GetDescription());
        }
        
        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
        {
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.PasswordInvalid, AuthenticationErrorCode.PasswordInvalid.GetDescription());
        }

        var tokenResult = _tokenGenerator.GenerateToken(user);
        var vm = new LoginResponseViewModel(user, tokenResult);

        return Result<LoginResponseViewModel>.Ok(vm);
    }
}
