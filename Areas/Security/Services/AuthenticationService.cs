using System.Threading.Tasks;
using Dapper;
using DynamicForm.Areas.Security.Models;
using DynamicForm.Areas.Security.Interfaces;
using DynamicForm.Areas.Security.ViewModels;
using Microsoft.Data.SqlClient;

namespace DynamicForm.Areas.Security.Services
{
    /// <summary>
    /// 使用者驗證服務。
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IDbExecutor _db;
        private readonly SqlConnection _connection;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenGenerator _tokenGenerator;

        /// <summary>
        /// 建構函式注入相依物件。
        /// </summary>
        /// <param name="connection">資料庫連線。</param>
        /// <param name="passwordHasher">密碼雜湊器。</param>
        /// <param name="tokenGenerator">Token 產生器。</param>
        public AuthenticationService(
            IDbExecutor db,
            SqlConnection connection,
            IPasswordHasher passwordHasher,
            ITokenGenerator tokenGenerator)
        {
            _db = db;
            _connection = connection;
            _passwordHasher = passwordHasher;
            _tokenGenerator = tokenGenerator;
        }

        /// <inheritdoc />
        public async Task<LoginResponseViewModel?> AuthenticateAsync(string account, string password, CancellationToken ct = default)
        {
            const string sql = @"/**/SELECT ID, NAME AS Account, SWD AS PasswordHash, SWD_SALT AS PasswordSalt FROM SYS_USER WHERE NAME = @Account AND IS_DELETE = 0";
            var user = await _db.QueryFirstOrDefaultAsync<UserAccount>(
                sql,
                new { Account = account },
                timeoutSeconds: 30,
                ct: ct
            );
            
            if (user == null)
            {
                return null;
            }

            if (!_passwordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
            {
                return null;
            }

            var tokenResult = _tokenGenerator.GenerateToken(user);
            return new LoginResponseViewModel
            {
                Token = tokenResult.Token,
                Expiration = tokenResult.Expiration,
                RefreshToken = tokenResult.RefreshToken,
                RefreshTokenExpiration = tokenResult.RefreshTokenExpiration
            };
        }
        
        public async Task<RegisterResponseViewModel?> RegisterAsync(string account, string password, CancellationToken ct = default)
        {
            // 1. 檢查帳號是否已存在
            const string checkSql = @"/**/SELECT COUNT(1) FROM SYS_USER WHERE NAME = @Account AND IS_DELETE = 0";
            var exists = await _db.ExecuteScalarAsync<int>(
                checkSql,   
                new { Account = account },
                timeoutSeconds: 30,
                ct: ct
            );
            
            if (exists > 0)
            {
                return null; // 帳號已存在
            }

            // 2. 生成鹽與雜湊
            var salt = _passwordHasher.GenerateSalt();
            var hash = _passwordHasher.HashPassword(password, salt);

            // 3. 寫入資料庫
            var userId = Guid.NewGuid();
            var role = "ADMIN";
            const string insertSql = @"/**/
        INSERT INTO SYS_USER (ID, AC, NAME, SWD, SWD_SALT, ROLE, IS_DELETE)
        VALUES (@Id, @AC, @Name, @Hash, @Salt, @Role, 0)";
            await _connection.ExecuteAsync(insertSql, new
            {
                Id = userId,
                AC = account,
                Name = account,
                Hash = hash,
                Salt = salt,
                Role = role
            });

            return new RegisterResponseViewModel
            {
                UserId = userId,
                Account = account,
                Role = role
            };
        }

    }
}
