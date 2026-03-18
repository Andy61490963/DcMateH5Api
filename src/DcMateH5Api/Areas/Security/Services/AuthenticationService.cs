using ClassLibrary;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.ViewModels;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using DbExtensions;
using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.Menu;

namespace DcMateH5Api.Areas.Security.Services;


public class AuthenticationService : Interfaces.IAuthenticationService
{
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _config;
    private readonly IMenuService _menuService;
    public AuthenticationService(
        SQLGenerateHelper sqlHelper,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration config,
        IMenuService menuService)
    {
        _sqlHelper = sqlHelper;
        _httpContextAccessor = httpContextAccessor;
        _config = config; 
        _menuService = menuService;
    }
    
    public async Task<Result<LoginResponseViewModel>> H5LoginAsync(string account, string password, CancellationToken ct = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var now = DateTime.Now;

        var where = new WhereBuilder<UserAccount>()
            .AndEq(x => x.Account, account);

        var user = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);

        if (user == null)
        {
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "帳號或密碼錯誤");
        }

        bool isValid = !string.IsNullOrWhiteSpace(user.PasswordHash)
            && !string.IsNullOrWhiteSpace(user.PasswordSalt)
            && PasswordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);

        if (!isValid)
        {
            var failedLoginLogSid = RandomHelper.GenerateRandomDecimal();
            var failedLoginLog = new UserLoginLogDto
            {
                ADM_USER_HIST_SID = failedLoginLogSid,
                ADM_USER_SID = user.Id,
                ACCOUNT_NO = user.Account,
                REPORT_TIME = now,
                LAST_ACTIVE_TIME = now,
                IP_ADDRESS = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                ACTION_CODE = "User login",
                ACTION_RESULT = false,
                ACTION_COMMENT = "User login failed: invalid password."
            };

            await _sqlHelper.InsertAsync(failedLoginLog, false, ct);

            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "帳號或密碼錯誤");
        }

        int expireMinutes = _config.GetValue<int>("AuthSettings:ExpireTimeSpanMinutes");

        var loginLogSid = RandomHelper.GenerateRandomDecimal();
        var loginLog = new UserLoginLogDto
        {
            ADM_USER_HIST_SID = loginLogSid,
            ADM_USER_SID = user.Id,
            ACCOUNT_NO = user.Account,
            REPORT_TIME = now,
            LAST_ACTIVE_TIME = now,
            IP_ADDRESS = httpContext?.Connection?.RemoteIpAddress?.ToString(),
            OPI_TYPE = "PC",
            ACTION_CODE = "User login",
            ACTION_RESULT = true,
            ACTION_COMMENT = "User login successfully."
        };

        await _sqlHelper.InsertAsync(loginLog, false, ct);

        string userLv = user.LV?.ToString() ?? "0";

        var claims = new List<Claim>
        {
            new (AppClaimTypes.Account, user.Account ?? string.Empty),
            new (AppClaimTypes.UserId, user.Id.ToString()),
            new (AppClaimTypes.UserLv, userLv),
            new ("LoginLogSid", loginLogSid.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(expireMinutes),
        };

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        var setCookieHeader = httpContext.Response.Headers["Set-Cookie"].ToString();
        string encryptedTicket = string.Empty;

        if (!string.IsNullOrEmpty(setCookieHeader))
        {
            encryptedTicket = ExtractTokenFromResponse();
        }

        string expiresFrom = now.ToString("yyyy-MM-dd HH:mm:ss");
        string expiresTo = authProperties.ExpiresUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;

        var menuData = await _menuService.GetFullMenuByLvAsync(user.LV ?? 0, user.Id);

        return Result<LoginResponseViewModel>.Ok(new LoginResponseViewModel
        {
            User = user.Account,
            LV = user.LV?.ToString() ?? "0",
            Sid = user.Id,
            Token = encryptedTicket,
            ExpiresFrom = expiresFrom,
            ExpiresTo = expiresTo,
            Menus = menuData
        });
    }
    
    /// <summary>
    /// 登出
    /// </summary>
    public async Task LogoutAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var now = DateTime.Now;

        try
        {
            if (user?.Identity?.IsAuthenticated == true)
            {
                var userIdStr = user.FindFirst(AppClaimTypes.UserId)?.Value;
                var account = user.FindFirst(AppClaimTypes.Account)?.Value ?? string.Empty;

                if (Guid.TryParse(userIdStr, out var userId))
                {
                    var logoutLogSid = RandomHelper.GenerateRandomDecimal();

                    var logoutLog = new UserLoginLogDto
                    {
                        ADM_USER_HIST_SID = logoutLogSid,
                        ADM_USER_SID = userId,
                        ACCOUNT_NO = account,
                        REPORT_TIME = now,
                        LAST_ACTIVE_TIME = now,
                        IP_ADDRESS = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                        OPI_TYPE = "PC",
                        ACTION_CODE = "User logout",
                        ACTION_RESULT = true,
                        ACTION_COMMENT = "User logout successfully."
                    };

                    await _sqlHelper.InsertAsync(logoutLog, false);
                }
            }

            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            try
            {
                if (user?.Identity?.IsAuthenticated == true)
                {
                    var userIdStr = user.FindFirst(AppClaimTypes.UserId)?.Value;
                    var account = user.FindFirst(AppClaimTypes.Account)?.Value ?? string.Empty;

                    if (Guid.TryParse(userIdStr, out var userId))
                    {
                        var failedLogoutLogSid = RandomHelper.GenerateRandomDecimal();
                        var errorMessage = ex.Message.Length > 200
                            ? ex.Message[..200]
                            : ex.Message;

                        var failedLogoutLog = new UserLoginLogDto
                        {
                            ADM_USER_HIST_SID = failedLogoutLogSid,
                            ADM_USER_SID = userId,
                            ACCOUNT_NO = account,
                            REPORT_TIME = now,
                            LAST_ACTIVE_TIME = now,
                            IP_ADDRESS = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                            OPI_TYPE = "PC",
                            ACTION_CODE = "User logout",
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

    private async Task<Result<bool>> RefreshAuthCookieAsync()
    {
        var user = _httpContextAccessor.HttpContext.User;

        if (user == null || !user.Identity.IsAuthenticated)
        {
            return Result<bool>.Fail(AuthenticationErrorCode.UserNotFound, "�����w�L���εL��");
        }
        
        var userId = user.FindFirst("UserId")?.Value;
        var userLv = user.FindFirst("UserLV")?.Value;
        var account = user.Identity.Name;

        // 4. ���s�ʸ� Claims
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, account),
        new Claim("UserId", userId),
        new Claim("UserLV", userLv),
        new Claim("RefreshTime", DateTime.Now.Ticks.ToString())
    };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true
        };

        await _httpContextAccessor.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<LoginResponseViewModel>> ExtendSessionAsync()
    {
        var refreshResult = await RefreshAuthCookieAsync();
        
        if (!refreshResult.IsSuccess)
        {
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.Unauthorized, refreshResult.Message);
        }
        
        var setCookieHeader = _httpContextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();
        string newTicket = "";
        if (setCookieHeader.Contains("DcMateAuthTicket="))
        {
            newTicket = ExtractTokenFromResponse();
        }
        
        return Result<LoginResponseViewModel>.Ok(new LoginResponseViewModel
        {
            Token = newTicket,
            ExpiresFrom = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ExpiresTo = DateTime.Now.AddMinutes(_config.GetValue<int>("AuthSettings:ExpireTimeSpanMinutes")).ToString("yyyy-MM-dd HH:mm:ss")
        });
    }
    
    private string ExtractTokenFromResponse()
    {
        var setCookieHeader = _httpContextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();

        if (!string.IsNullOrEmpty(setCookieHeader) && setCookieHeader.Contains("DcMateAuthTicket="))
        {
            return setCookieHeader.Split(';')[0].Replace("DcMateAuthTicket=", "");
        }

        return string.Empty;
    }
}