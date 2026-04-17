using ClassLibrary;
using DbExtensions;
using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.Menu;
using DcMateH5.Abstractions.Menu.Models;
using DcMateH5.Abstractions.RegistrationLicense;
using DcMateH5.Abstractions.RegistrationLicense.Model;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.ViewModels.Login;
using DcMateH5Api.Models;
using System.Security.Claims;
using DcMateH5.Abstractions.Token;
using DcMateH5.Abstractions.Token.Model;

namespace DcMateH5Api.Areas.Security.Services;

public class AuthenticationService : Interfaces.IAuthenticationService
{
    private static class AuthConstants
    {
        public const string OpiTypePc = "PC";
        public const string LoginActionCode = "User login";
        public const string LogoutActionCode = "User logout";
        public const string LoginSuccessComment = "User login successfully.";
        public const string LoginFailedComment = "User login failed: invalid password.";
        public const string TokenStatusSuccess = "true";
        public const string TokenStatusFail = "false";
        public const int MinTokenSeq = 1;
        public const int MaxTokenSeq = 999;
    }

    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _config;
    private readonly IMenuService _menuService;
    private readonly IRegistrationLicenseService _registrationLicenseService;
    private readonly ITokenService _tokenService;

    public AuthenticationService(
            SQLGenerateHelper sqlHelper,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration config,
            IMenuService menuService,
            IRegistrationLicenseService registrationLicenseService,
            ITokenService tokenService)
        {
            _sqlHelper = sqlHelper;
            _httpContextAccessor = httpContextAccessor;
            _config = config;
            _menuService = menuService;
            _registrationLicenseService = registrationLicenseService;
            _tokenService = tokenService;
        }

    public async Task<Result<LoginResponseViewModel>> H5LoginAsync(string account, string password, CancellationToken ct = default)
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        DateTime now = DateTime.Now;

        if (httpContext == null)
        {
            return Result<LoginResponseViewModel>.Fail(
                AuthenticationErrorCode.Unauthorized,
                "Login context is not available.");
        }

        CfgRegisterDto? cfgRegister = await GetCfgRegisterAsync(ct);
        if (cfgRegister == null)
        {
            return Result<LoginResponseViewModel>.Fail(
                AuthenticationErrorCode.CfgRegisterNotFound,
                "License validation failed.");
        }

        LicenseParseResponse licenseResult = ParseLicense(cfgRegister);
        if (!licenseResult.VerifyResult)
        {
            LoginResponseViewModel failedLicenseResponse = BuildFailedResponse(
                account,
                licenseResult,
                "License validation failed.");

            return Result<LoginResponseViewModel>.Fail(
                AuthenticationErrorCode.CfgRegisterNotFound,
                "License validation failed.",
                failedLicenseResponse);
        }

        if (await IsAccountLockedAsync(account, ct))
        {
            return Result<LoginResponseViewModel>.Fail(
                AuthenticationErrorCode.AccountLocked,
                $"Account locked. Try again after {_config.GetValue<int>("LoginOptions:LockoutMinutes")} minutes.");
        }
        
        UserAccount? user = await GetUserByAccountAsync(account, ct);
        if (user == null)
        {
            LoginResponseViewModel failedUserResponse = BuildFailedResponse(
                account,
                licenseResult,
                "Invalid account or password.");

            return Result<LoginResponseViewModel>.Fail(
                AuthenticationErrorCode.UserNotFound,
                "Invalid account or password.",
                failedUserResponse);
        }

        bool isValidPassword = VerifyPassword(user, password);
        if (!isValidPassword)
        {
            await WriteLoginLogAsync(
                user,
                now,
                false,
                AuthConstants.LoginFailedComment,
                ct);

            LoginResponseViewModel failedPasswordResponse = BuildFailedResponse(
                user.Account,
                licenseResult,
                "Invalid account or password.");

            return Result<LoginResponseViewModel>.Fail(
                AuthenticationErrorCode.UserNotFound,
                "Invalid account or password.",
                failedPasswordResponse);
        }

        // 錯誤測試
        //throw new InvalidOperationException("Mock login exception for testing.");
        
        int expireMinutes = _config.GetValue<int>("TokenOptions:DefaultTokenKeyMinutes");
        
        int nextTokenSeq = await GetNextTokenSeqAsync(ct);
        
        TokenPayload tokenPayload = new TokenPayload
        {
            Domain = string.Empty,
            TokenMinutes = expireMinutes,
            TokenSeq = nextTokenSeq,
            UserId = user.Id,
            Account = user.Account,
            SessionId = Guid.NewGuid().ToString("N"),
            UserLv = user.Lv
        };

        GenerateTokenResult tokenResult = _tokenService.GenerateToken(tokenPayload);
        AuthInfo authInfo = await _menuService.GetFullMenuByLvAsync(user.Lv ?? "0", user.Id);

        LoginResponseViewModel successResponse = BuildSuccessResponse(
            user,
            cfgRegister,
            licenseResult,
            authInfo,
            tokenResult.TokenKey,
            tokenResult.Expiration,
            nextTokenSeq);
        
        decimal loginLogSid = await WriteLoginLogAsync(
            user,
            now,
            true,
            AuthConstants.LoginSuccessComment,
            ct);
        
        return Result<LoginResponseViewModel>.Ok(successResponse);
    }

    /// <summary>
    /// 登出
    /// </summary>
    public async Task LogoutAsync()
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        ClaimsPrincipal? user = httpContext?.User;
        DateTime now = DateTime.Now;

        try
        {
            if (user?.Identity?.IsAuthenticated == true)
            {
                string? userIdStr = user.FindFirst(AppClaimTypes.UserId)?.Value;
                string account = user.FindFirst(AppClaimTypes.Account)?.Value ?? string.Empty;

                if (Guid.TryParse(userIdStr, out Guid userId))
                {
                    decimal logoutLogSid = RandomHelper.GenerateRandomDecimal();

                    UserLoginLogDto logoutLog = new UserLoginLogDto
                    {
                        ADM_USER_HIST_SID = logoutLogSid,
                        ADM_USER_SID = userId,
                        ACCOUNT_NO = account,
                        REPORT_TIME = now,
                        LAST_ACTIVE_TIME = now,
                        IP_ADDRESS = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                        OPI_TYPE = AuthConstants.OpiTypePc,
                        ACTION_CODE = AuthConstants.LogoutActionCode,
                        ACTION_RESULT = true,
                        ACTION_COMMENT = "User logout successfully."
                    };

                    await _sqlHelper.InsertAsync(logoutLog, false);
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (user?.Identity?.IsAuthenticated == true)
                {
                    string? userIdStr = user.FindFirst(AppClaimTypes.UserId)?.Value;
                    string account = user.FindFirst(AppClaimTypes.Account)?.Value ?? string.Empty;

                    if (Guid.TryParse(userIdStr, out Guid userId))
                    {
                        decimal failedLogoutLogSid = RandomHelper.GenerateRandomDecimal();
                        string errorMessage = ex.Message.Length > 200
                            ? ex.Message[..200]
                            : ex.Message;

                        UserLoginLogDto failedLogoutLog = new UserLoginLogDto
                        {
                            ADM_USER_HIST_SID = failedLogoutLogSid,
                            ADM_USER_SID = userId,
                            ACCOUNT_NO = account,
                            REPORT_TIME = now,
                            LAST_ACTIVE_TIME = now,
                            IP_ADDRESS = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                            OPI_TYPE = AuthConstants.OpiTypePc,
                            ACTION_CODE = AuthConstants.LogoutActionCode,
                            ACTION_RESULT = false,
                            ACTION_COMMENT = $"User logout failed: {errorMessage}"
                        };

                        await _sqlHelper.InsertAsync(failedLogoutLog);
                    }
                }
            }
            catch
            {
                // 避免 logging failure 影響主流程
            }

            throw;
        }
    }

    public async Task<Result<LoginResponseViewModel>> AuthenticateAsync(string account, string password, CancellationToken ct = default)
    {
        return await H5LoginAsync(account, password, ct);
    }

    private async Task<CfgRegisterDto?> GetCfgRegisterAsync(CancellationToken ct)
    {
        WhereBuilder<CfgRegisterDto> whereCfgRegister = new WhereBuilder<CfgRegisterDto>();
        return await _sqlHelper.SelectFirstOrDefaultAsync(whereCfgRegister, ct);
    }

    private LicenseParseResponse ParseLicense(CfgRegisterDto cfgRegister)
    {
        if (string.IsNullOrWhiteSpace(cfgRegister.REGCODE))
        {
            return new LicenseParseResponse
            {
                VerifyResult = false,
                ResultMessage = "License Code is Empty."
            };
        }

        return _registrationLicenseService.Parse(cfgRegister.REGCODE, cfgRegister.CHECK_CODE);
    }

    private async Task<UserAccount?> GetUserByAccountAsync(string account, CancellationToken ct)
    {
        WhereBuilder<UserAccount> whereUser = new WhereBuilder<UserAccount>()
            .AndEq(x => x.Account, account);

        return await _sqlHelper.SelectFirstOrDefaultAsync(whereUser, ct);
    }

    private async Task<bool> IsAccountLockedAsync(string account, CancellationToken ct)
    {
        const string sql = @"
SELECT COUNT(1)
FROM ADM_USER_HIST
WHERE ACCOUNT_NO = @Account
AND ACTION_CODE = @ActionCode
AND ACTION_RESULT = 0
AND REPORT_TIME >= DATEADD(MINUTE, -@LockoutMinutes, GETDATE())
AND REPORT_TIME > ISNULL(
    (
        SELECT MAX(t.USED_TIME)
        FROM ADM_PASSWORD_RESET_TOKEN t
        JOIN ADM_USER u ON u.USER_SID = t.USER_SID
        WHERE u.ACCOUNT_NO = @Account
          AND t.USED_TIME IS NOT NULL
    ),
    CONVERT(datetime2(3), '1900-01-01')
);";

        var maxFailedAttempts = _config.GetValue<int>("LoginOptions:MaxFailedAttempts");
        var lockoutMinutes = _config.GetValue<int>("LoginOptions:LockoutMinutes");
        
        int failedCount = await _sqlHelper.ExecuteScalarAsync<int>(
            sql,
            new
            {
                Account = account,
                ActionCode = AuthConstants.LoginActionCode,
                lockoutMinutes
            },
            ct);

        return failedCount >= maxFailedAttempts;
    }
    
    private static bool VerifyPassword(UserAccount user, string password)
    {
        return !string.IsNullOrWhiteSpace(user.PasswordHash)
               && !string.IsNullOrWhiteSpace(user.PasswordSalt)
               && PasswordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);
    }

    private async Task<decimal> WriteLoginLogAsync(
        UserAccount user,
        DateTime now,
        bool actionResult,
        string actionComment,
        CancellationToken ct)
    {
        decimal loginLogSid = RandomHelper.GenerateRandomDecimal();

        UserLoginLogDto loginLog = new UserLoginLogDto
        {
            ADM_USER_HIST_SID = loginLogSid,
            ADM_USER_SID = user.Id,
            ACCOUNT_NO = user.Account,
            REPORT_TIME = now,
            LAST_ACTIVE_TIME = now,
            IP_ADDRESS = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
            OPI_TYPE = AuthConstants.OpiTypePc,
            ACTION_CODE = AuthConstants.LoginActionCode,
            ACTION_RESULT = actionResult,
            ACTION_COMMENT = actionComment
        };

        await _sqlHelper.InsertAsync(loginLog, false, ct);

        return loginLogSid;
    }

    private LoginResponseViewModel BuildSuccessResponse(
        UserAccount user,
        CfgRegisterDto cfgRegister,
        LicenseParseResponse licenseResult,
        AuthInfo authInfo,
        string token,
        DateTime tokenExpiry,
        int tokenSeq)
    {
        return new LoginResponseViewModel
        {
            tokenInfo = BuildTokenInfo(user.Account, token, tokenExpiry, tokenSeq, true),
            userInfo = BuildUserInfo(user, cfgRegister, licenseResult, true, "Login succeeded."),
            authInfo = authInfo
        };
    }

    private LoginResponseViewModel BuildFailedResponse(
        string account,
        LicenseParseResponse licenseResult,
        string loginMessage)
    {
        return new LoginResponseViewModel
        {
            tokenInfo = new TokenInfo
            {
                TOKEN_STATUS = AuthConstants.TokenStatusFail,
                ACCOUNT_NO = account,
                TOKEN_KEY = string.Empty,
                TOKEN_EXPIRY = null,
                TOKEN_SEQ = 0
            },
            userInfo = new UserInfo
            {
                ACCOUNT_NO = account,
                NICKNAME = string.Empty,
                EMP_NO = string.Empty,
                DEPT_SID = "0",
                TITLE_SID = "0",
                SECURITY_ID = "0",
                COMPANY = string.Empty,
                LV = "0",
                REG_DATABASE = licenseResult.DbDataSource ?? string.Empty,
                REG_EXPIRE_DATE = licenseResult.ExpiredDate ?? string.Empty,
                REG_CURR_USER_LIM = licenseResult.NumOfReg ?? string.Empty,
                REG_CURR_USER = "0",
                REG_COMPANY = licenseResult.CustomerName ?? string.Empty,
                REG_MSG = licenseResult.ResultMessage ?? string.Empty,
                LOGIN_STATUS = AuthConstants.TokenStatusFail,
                LOGIN_MSG = loginMessage
            },
            authInfo = new AuthInfo()
        };
    }

    private TokenInfo BuildTokenInfo(
        string account,
        string token,
        DateTime tokenExpiry,
        int tokenSeq,
        bool isSuccess)
    {
        return new TokenInfo
        {
            TOKEN_STATUS = isSuccess ? AuthConstants.TokenStatusSuccess : AuthConstants.TokenStatusFail,
            ACCOUNT_NO = account,
            TOKEN_KEY = token,
            TOKEN_EXPIRY = tokenExpiry,
            TOKEN_SEQ = tokenSeq
        };
    }

    private UserInfo BuildUserInfo(
        UserAccount user,
        CfgRegisterDto cfgRegister,
        LicenseParseResponse licenseResult,
        bool loginSuccess,
        string loginMessage)
    {
        return new UserInfo
        {
            ACCOUNT_NO = user.Account ?? string.Empty,
            NICKNAME = user.NickName ?? string.Empty,
            EMP_NO = user.EmpNo ?? string.Empty,
            DEPT_SID = user.DeptSid.ToString(),
            TITLE_SID = user.TitleSid.ToString(),
            SECURITY_ID = user.SecurityId.ToString(),
            COMPANY = user.Company ?? string.Empty,
            LV = user.Lv ?? "0",
            REG_DATABASE = licenseResult.DbDataSource ?? string.Empty,
            REG_EXPIRE_DATE = licenseResult.ExpiredDate ?? string.Empty,
            REG_CURR_USER_LIM = licenseResult.NumOfReg ?? string.Empty,
            REG_CURR_USER = "0",
            REG_COMPANY = string.IsNullOrWhiteSpace(licenseResult.CustomerName)
                ? cfgRegister.CUSTOMER_NAME ?? string.Empty
                : licenseResult.CustomerName,
            REG_MSG = licenseResult.ResultMessage ?? string.Empty,
            LOGIN_STATUS = loginSuccess ? AuthConstants.TokenStatusSuccess : AuthConstants.TokenStatusFail,
            LOGIN_MSG = loginMessage
        };
    }

    private async Task<int> GetNextTokenSeqAsync(CancellationToken ct)
    {
        const string sql = @"
UPDATE CFG_REGISTER
SET TOKEN_SEQ =
    CASE
        WHEN ISNULL(TOKEN_SEQ, 0) >= @MaxTokenSeq THEN @MinTokenSeq
        WHEN ISNULL(TOKEN_SEQ, 0) < @MinTokenSeq THEN @MinTokenSeq
        ELSE TOKEN_SEQ + 1
    END
OUTPUT INSERTED.TOKEN_SEQ;";
        
        int nextTokenSeq = await _sqlHelper.ExecuteScalarAsync<int>(
            sql,
            new
            {
                MinTokenSeq = AuthConstants.MinTokenSeq,
                MaxTokenSeq = AuthConstants.MaxTokenSeq
            },
            ct);

        return nextTokenSeq;
    }
}
