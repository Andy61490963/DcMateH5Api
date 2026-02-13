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
        var where = new WhereBuilder<UserAccount>()
            .AndEq(x => x.Account, account);

        var user = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);

        if (user == null)
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "bαKX~");
        
        bool isValid = PasswordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);

        if (!isValid)
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "帳號或密碼錯誤");

        // ---------------------------------------------------------
        // Concurrent Login Check
        // ---------------------------------------------------------
        int expireMinutes = _config.GetValue<int>("AuthSettings:ExpireTimeSpanMinutes");
        
        // 先暫時不卡使用者登入數量
        // int maxConcurrentLogins = _config.GetValue<int>("AuthSettings:MaxConcurrentLogins", 3); // Default 3 if not set
        //
        // var cutoffTime = DateTime.Now.AddMinutes(-expireMinutes);
        //
        // // Count active sessions (GLOBAL)
        // var countWhere = new WhereBuilder<UserLoginLogDto>()
        //     .AndEq(x => x.LOGOUT_TIME, null)
        //     .AndGt(x => x.LAST_ACTIVE_TIME, cutoffTime);
        //
        // var activeCount = await _sqlHelper.SelectCountAsync(countWhere, ct);
        //
        // if (activeCount >= maxConcurrentLogins)
        // {
        //      return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.Unauthorized, $"已達最大同時登入限制 ({maxConcurrentLogins})，請稍後再試或登出其他裝置。");
        // }

        // Create Login Log
        var loginLogSid = Guid.NewGuid();
        var loginLog = new UserLoginLogDto
        {
            ADM_USER_LOGIN_LOG_SID = loginLogSid,
            ADM_USER_SID = user.Id,
            ACCOUNT_NO = user.Account,
            LOGIN_TIME = DateTime.Now,
            LAST_ACTIVE_TIME = DateTime.Now,
            IP_ADDRESS = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        await _sqlHelper.InsertAsync(loginLog, ct);

        // ---------------------------------------------------------

        string userLv = user.LV?.ToString() ?? "0";

        var claims = new List<Claim>
        {
            new Claim(AppClaimTypes.Account, user.Account),
            new Claim(AppClaimTypes.UserId, user.Id.ToString()),
            new Claim(AppClaimTypes.UserLv, userLv),
            new Claim("LoginLogSid", loginLogSid.ToString()) // Add Login Log SID claim
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
       
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, 
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(expireMinutes),
        };

        await _httpContextAccessor.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
        
        var setCookieHeader = _httpContextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();
        string encryptedTicket = string.Empty;
        if (!string.IsNullOrEmpty(setCookieHeader))
        {
            var ticketPart = setCookieHeader.Split(';')[0]; 
            encryptedTicket = ExtractTokenFromResponse();
        }
        
        string expiresFrom = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); 
        string expiresTo = authProperties.ExpiresUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        
        var menuData = await _menuService.GetFullMenuByLvAsync(user.LV ?? 0);

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

    public async Task LogoutAsync()
    {
        var user = _httpContextAccessor.HttpContext.User;
        if (user.Identity.IsAuthenticated)
        {
            var loginLogSidStr = user.FindFirst("LoginLogSid")?.Value;
            if (Guid.TryParse(loginLogSidStr, out var loginLogSid))
            {
                await _sqlHelper.UpdateById<UserLoginLogDto>(loginLogSid)
                    .Set(x => x.LOGOUT_TIME, DateTime.Now)
                    .ExecuteAsync();
            }
        }

        await _httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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