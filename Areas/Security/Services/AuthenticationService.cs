using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.ViewModels;
using DcMateH5Api.Areas.Security.Mappers;
using DcMateH5Api.SqlHelper;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Security.Services
{
    /// <summary>
    /// 使用者驗證服務。
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly SQLGenerateHelper _sqlHelper;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenGenerator _tokenGenerator;

        /// <summary>
        /// 建構函式注入相依物件。
        /// </summary>
        /// <param name="sqlHelper">sql產生器。</param>
        /// <param name="passwordHasher">密碼雜湊器。</param>
        /// <param name="tokenGenerator">Token 產生器。</param>
        public AuthenticationService(
            SQLGenerateHelper sqlHelper,
            IPasswordHasher passwordHasher,
            ITokenGenerator tokenGenerator)
        {
            _sqlHelper = sqlHelper;
            _passwordHasher = passwordHasher;
            _tokenGenerator = tokenGenerator;
        }

        /// <summary>
        /// 登入檢查流程
        /// </summary>
        /// <param name="account"></param>
        /// <param name="password"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<LoginResponseViewModel?> AuthenticateAsync(string account, string password, CancellationToken ct = default)
        {
            var where = new WhereBuilder<UserAccount>()
                .AndEq(x => x.Account, account)
                .AndNotDeleted();
            
            var user = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
            
            if (user == null)
            {
                return null;
            }

            if (!_passwordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
            {
                return null;
            }

            var tokenResult = _tokenGenerator.GenerateToken(user);
            return new LoginResponseViewModel(user, tokenResult);
        }
        
        /// <summary>
        /// 註冊帳號
        /// </summary>
        public async Task<int> RegisterAsync(RegisterRequestViewModel request, CancellationToken ct = default)
        {
            var where = new WhereBuilder<UserAccount>()
                .AndEq(x => x.Account, request.Account)
                .AndNotDeleted();

            var user = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);

            if (user != null)
            {
                return -1; // -1 代表帳號已存在
            }

            var salt = _passwordHasher.GenerateSalt();
            var model = UserAccountMapper.MapperRegisterAndDto(request, salt, _passwordHasher);

            var rows = await _sqlHelper.InsertAsync(model, ct);
            return rows; // >0 = 成功, 0 = 失敗
        }
    }
}
