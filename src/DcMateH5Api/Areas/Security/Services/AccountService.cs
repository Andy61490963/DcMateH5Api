using System.Security.Cryptography;
using System.Text;
using ClassLibrary;
using DbExtensions.DbExecutor.Interface;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Models;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.Options;
using DcMateH5Api.Areas.Security.ViewModels.Password;
using DcMateH5Api.Areas.Security.ViewModels.Register;
using DcMateH5Api.Models;
using Microsoft.Extensions.Options;

namespace DcMateH5Api.Areas.Security.Services;

/// <summary>
/// Account service.
/// </summary>
public sealed class AccountService : IAccountService
{
    private static class SqlStatements
    {
        public const string AccountExists = @"
SELECT COUNT(1)
FROM ADM_USER
WHERE ACCOUNT_NO = @Account;";

        public const string InsertUser = @"
INSERT INTO ADM_USER
(
    USER_SID,
    ACCOUNT_NO,
    PWD,
    SECOND_PWD,
    USER_NAME,
    NICKNAME,
    EMP_NO,
    COMPANY,
    DEPT_SID,
    TITLE_SID,
    ENABLE_FLAG,
    EMAIL,
    CREATE_USER,
    CREATE_TIME,
    EDIT_USER,
    EDIT_TIME,
    LV
)
VALUES
(
    @UserId,
    @Account,
    @PasswordHash,
    @PasswordSalt,
    @Name,
    @NickName,
    @EmpNo,
    @Company,
    @DeptSid,
    @TitleSid,
    @EnableFlag,
    @Email,
    @Actor,
    SYSDATETIME(),
    @Actor,
    SYSDATETIME(),
    @Lv
);";

        public const string ResetPassword = @"
UPDATE ADM_USER
SET
    PWD = @PasswordHash,
    SECOND_PWD = @PasswordSalt,
    EDIT_USER = @Actor,
    EDIT_TIME = SYSDATETIME()
WHERE ACCOUNT_NO = @Account;";

        public const string SelectForgotPasswordUser = @"
SELECT TOP (1)
    USER_SID AS UserId,
    ACCOUNT_NO AS Account,
    EMAIL AS Email
FROM ADM_USER
WHERE ACCOUNT_NO = @Account;";

        public const string InsertPasswordResetToken = @"
INSERT INTO ADM_PASSWORD_RESET_TOKEN
(
    PASSWORD_RESET_TOKEN_SID,
    USER_SID,
    TOKEN_HASH,
    EXPIRED_TIME,
    CREATE_TIME,
    CREATE_IP
)
VALUES
(
    @TokenId,
    @UserId,
    @TokenHash,
    @ExpiredTime,
    SYSDATETIME(),
    @CreateIp
);";

        public const string SelectPasswordResetToken = @"
SELECT TOP (1)
    u.ACCOUNT_NO AS Account,
    t.EXPIRED_TIME AS ExpiredTime
FROM ADM_PASSWORD_RESET_TOKEN t
JOIN ADM_USER u ON u.USER_SID = t.USER_SID
WHERE t.TOKEN_HASH = @TokenHash
  AND t.USED_TIME IS NULL
  AND t.EXPIRED_TIME >= SYSDATETIME();";

        public const string ConsumePasswordResetToken = @"
UPDATE ADM_PASSWORD_RESET_TOKEN
SET USED_TIME = SYSDATETIME()
OUTPUT INSERTED.USER_SID
WHERE TOKEN_HASH = @TokenHash
  AND USED_TIME IS NULL
  AND EXPIRED_TIME >= SYSDATETIME();";

        public const string SelectAccountByUserId = @"
SELECT TOP (1) ACCOUNT_NO
FROM ADM_USER
WHERE USER_SID = @UserId;";

        public const string ResetPasswordByUserId = @"
UPDATE ADM_USER
SET
    PWD = @PasswordHash,
    SECOND_PWD = @PasswordSalt,
    EDIT_USER = @Actor,
    EDIT_TIME = SYSDATETIME()
WHERE USER_SID = @UserId;";
    }

    private static class DefaultValues
    {
        public const string EnableFlag = "Y";
        public const string Company = "0";
        public const decimal DeptSid = 0;
        public const decimal TitleSid = 0;
    }

    private readonly IDbExecutor _db;
    private readonly IEmailSender _emailSender;
    private readonly PasswordResetOptions _passwordResetOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PasswordHasher _passwordHasher;

    public AccountService(
        IDbExecutor db,
        IEmailSender emailSender,
        IOptions<PasswordResetOptions> passwordResetOptions,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _emailSender = emailSender;
        _passwordResetOptions = passwordResetOptions.Value;
        _httpContextAccessor = httpContextAccessor;
        _passwordHasher = new PasswordHasher();
    }

    public async Task<Result<RegisterResponseViewModel>> RegisterAsync(
        RegisterRequestViewModel request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Account) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<RegisterResponseViewModel>.Fail(
                AccountErrorCode.InvalidRequest,
                "Account and Password are required.");
        }

        string account = request.Account.Trim();
        string password = request.Password.Trim();
        string? email = NormalizeOptional(request.Email);
        int lv = request.Lv;

        if (lv <= 0)
        {
            return Result<RegisterResponseViewModel>.Fail(
                AccountErrorCode.InvalidRequest,
                "Lv must be greater than 0.");
        }

        if (await AccountExistsAsync(account, ct))
        {
            return Result<RegisterResponseViewModel>.Fail(
                AuthenticationErrorCode.AccountAlreadyExists,
                "Account already exists.");
        }

        (string passwordHash, string passwordSalt) = CreatePasswordHash(password);
        Guid userId = Guid.NewGuid();

        await _db.ExecuteAsync(
            SqlStatements.InsertUser,
            new
            {
                UserId = userId,
                Account = account,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Name = account,
                NickName = string.Empty,
                EmpNo = string.Empty,
                Company = DefaultValues.Company,
                DeptSid = DefaultValues.DeptSid,
                TitleSid = DefaultValues.TitleSid,
                EnableFlag = DefaultValues.EnableFlag,
                Email = email,
                Actor = account,
                Lv = lv
            },
            ct: ct);

        return Result<RegisterResponseViewModel>.Ok(
            new RegisterResponseViewModel
            {
                UserId = userId,
                Account = account,
                Name = account,
                Email = email,
                Lv = lv
            });
    }

    public async Task<Result<ResetUserPasswordResponseViewModel>> ResetPasswordAsync(
        ResetUserPasswordRequestViewModel request,
        string actor,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Account) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Result<ResetUserPasswordResponseViewModel>.Fail(
                AccountErrorCode.InvalidRequest,
                "Account and NewPassword are required.");
        }

        string account = request.Account.Trim();
        string newPassword = request.NewPassword.Trim();
        string editUser = NormalizeActor(actor);
        (string passwordHash, string passwordSalt) = CreatePasswordHash(newPassword);

        int affectedRows = await _db.ExecuteAsync(
            SqlStatements.ResetPassword,
            new
            {
                Account = account,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Actor = editUser
            },
            ct: ct);

        if (affectedRows == 0)
        {
            return Result<ResetUserPasswordResponseViewModel>.Fail(
                AuthenticationErrorCode.UserNotFound,
                "Account not found.");
        }

        return Result<ResetUserPasswordResponseViewModel>.Ok(
            new ResetUserPasswordResponseViewModel
            {
                Account = account
            });
    }

    public async Task<Result<ForgotPasswordResponseViewModel>> ForgotPasswordAsync(
        ForgotPasswordRequestViewModel request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Account))
        {
            return Result<ForgotPasswordResponseViewModel>.Fail(
                AccountErrorCode.InvalidRequest,
                "Account is required.");
        }

        string account = request.Account.Trim();
        ForgotPasswordUserRow? user = await _db.QueryFirstOrDefaultAsync<ForgotPasswordUserRow>(
            SqlStatements.SelectForgotPasswordUser,
            new { Account = account },
            ct: ct);

        if (user == null)
        {
            return Result<ForgotPasswordResponseViewModel>.Fail(
                AuthenticationErrorCode.UserNotFound,
                "Account not found.");
        }

        string? email = NormalizeOptional(user.Email);
        if (email == null)
        {
            return Result<ForgotPasswordResponseViewModel>.Fail(
                AccountErrorCode.EmailNotBound,
                "此帳號尚未綁定 Email，請聯絡管理員修改密碼。");
        }

        string token = CreateToken();
        string tokenHash = HashToken(token);
        DateTime expiredTime = DateTime.Now.AddMinutes(Math.Max(1, _passwordResetOptions.TokenMinutes));

        await _db.ExecuteAsync(
            SqlStatements.InsertPasswordResetToken,
            new
            {
                TokenId = Guid.NewGuid(),
                user.UserId,
                TokenHash = tokenHash,
                ExpiredTime = expiredTime,
                CreateIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
            },
            ct: ct);

        try
        {
            await _emailSender.SendAsync(
                email,
                "DcMate 密碼重設",
                BuildForgotPasswordEmailBody(user.Account, token, expiredTime),
                isHtml: true,
                ct);
        }
        catch
        {
            return Result<ForgotPasswordResponseViewModel>.Fail(
                AccountErrorCode.EmailSendFailed,
                "Password reset email could not be sent.");
        }

        return Result<ForgotPasswordResponseViewModel>.Ok(
            new ForgotPasswordResponseViewModel
            {
                Account = user.Account,
                Email = MaskEmail(email),
                ExpiredTime = expiredTime
            });
    }

    public async Task<Result<VerifyForgotPasswordTokenResponseViewModel>> VerifyForgotPasswordTokenAsync(
        string token,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<VerifyForgotPasswordTokenResponseViewModel>.Fail(
                AccountErrorCode.InvalidRequest,
                "Token is required.");
        }

        PasswordResetTokenRow? row = await _db.QueryFirstOrDefaultAsync<PasswordResetTokenRow>(
            SqlStatements.SelectPasswordResetToken,
            new { TokenHash = HashToken(token.Trim()) },
            ct: ct);

        if (row == null)
        {
            return Result<VerifyForgotPasswordTokenResponseViewModel>.Fail(
                AccountErrorCode.InvalidOrExpiredToken,
                "重設密碼連結已失效或不存在。");
        }

        return Result<VerifyForgotPasswordTokenResponseViewModel>.Ok(
            new VerifyForgotPasswordTokenResponseViewModel
            {
                Account = row.Account,
                ExpiredTime = row.ExpiredTime
            });
    }

    public async Task<Result<ResetForgotPasswordResponseViewModel>> ResetForgotPasswordAsync(
        ResetForgotPasswordRequestViewModel request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Result<ResetForgotPasswordResponseViewModel>.Fail(
                AccountErrorCode.InvalidRequest,
                "Token and NewPassword are required.");
        }

        string tokenHash = HashToken(request.Token.Trim());
        string newPassword = request.NewPassword.Trim();
        (string passwordHash, string passwordSalt) = CreatePasswordHash(newPassword);

        ResetForgotPasswordResponseViewModel? response = await _db.TxAsync(
            async (conn, tx, txCt) =>
            {
                Guid? userId = await _db.ExecuteScalarInTxAsync<Guid?>(
                    conn,
                    tx,
                    SqlStatements.ConsumePasswordResetToken,
                    new { TokenHash = tokenHash },
                    ct: txCt);

                if (userId == null)
                {
                    return null;
                }

                string? account = await _db.ExecuteScalarInTxAsync<string>(
                    conn,
                    tx,
                    SqlStatements.SelectAccountByUserId,
                    new { UserId = userId.Value },
                    ct: txCt);

                if (string.IsNullOrWhiteSpace(account))
                {
                    return null;
                }

                await _db.ExecuteInTxAsync(
                    conn,
                    tx,
                    SqlStatements.ResetPasswordByUserId,
                    new
                    {
                        UserId = userId.Value,
                        PasswordHash = passwordHash,
                        PasswordSalt = passwordSalt,
                        Actor = "PASSWORD_RESET"
                    },
                    ct: txCt);

                return new ResetForgotPasswordResponseViewModel
                {
                    Account = account
                };
            },
            ct: ct);

        if (response == null)
        {
            return Result<ResetForgotPasswordResponseViewModel>.Fail(
                AccountErrorCode.InvalidOrExpiredToken,
                "重設密碼連結已失效或不存在。");
        }

        return Result<ResetForgotPasswordResponseViewModel>.Ok(response);
    }

    private async Task<bool> AccountExistsAsync(string account, CancellationToken ct)
    {
        int? count = await _db.ExecuteScalarAsync<int>(
            SqlStatements.AccountExists,
            new { Account = account },
            ct: ct);

        return count > 0;
    }

    private (string PasswordHash, string PasswordSalt) CreatePasswordHash(string password)
    {
        string salt = _passwordHasher.GenerateSalt();
        string hash = _passwordHasher.HashPassword(password, salt);
        return (hash, salt);
    }

    private string BuildForgotPasswordEmailBody(string account, string token, DateTime expiredTime)
    {
        string verifyUrl = BuildVerifyUrl(token);
        return $"""
                <p>帳號：{System.Net.WebUtility.HtmlEncode(account)}</p>
                <p>請使用下方Token重設密碼：</p>
                <p>Token：{System.Net.WebUtility.HtmlEncode(token)}</p>
                <p>此 token 將於 {expiredTime:yyyy-MM-dd HH:mm:ss} 失效。</p>
                """;
    }

    private string BuildVerifyUrl(string token)
    {
        string encodedToken = Uri.EscapeDataString(token);
        if (!string.IsNullOrWhiteSpace(_passwordResetOptions.ResetUrl))
        {
            string separator = _passwordResetOptions.ResetUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{_passwordResetOptions.ResetUrl}{separator}token={encodedToken}";
        }

        HttpRequest? request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            return $"/Security/Account/password/forgot/verify?token={encodedToken}";
        }

        return $"{request.Scheme}://{request.Host}/Security/Account/password/forgot/verify?token={encodedToken}";
    }

    private static string NormalizeActor(string actor)
    {
        return string.IsNullOrWhiteSpace(actor)
            ? CurrentUserSnapshot.NotLoginUser
            : actor.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string CreateToken()
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return token.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private static string MaskEmail(string email)
    {
        int atIndex = email.IndexOf('@');
        if (atIndex <= 1)
        {
            return $"*{email[atIndex..]}";
        }

        string name = email[..atIndex];
        return $"{name[0]}***{email[atIndex..]}";
    }

    private sealed class ForgotPasswordUserRow
    {
        public Guid UserId { get; set; }

        public string Account { get; set; } = string.Empty;

        public string? Email { get; set; }
    }

    private sealed class PasswordResetTokenRow
    {
        public string Account { get; set; } = string.Empty;

        public DateTime ExpiredTime { get; set; }
    }

    private enum AccountErrorCode
    {
        InvalidRequest,
        EmailNotBound,
        InvalidOrExpiredToken,
        EmailSendFailed
    }
}
