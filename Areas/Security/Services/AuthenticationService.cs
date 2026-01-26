using ClassLibrary;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.ViewModels;
using DcMateH5Api.Models;
using DcMateH5Api.SqlHelper;
using DCMATEH5API.Areas.Menu.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace DcMateH5Api.Areas.Security.Services;


public class AuthenticationService : Interfaces.IAuthenticationService
{
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPasswordHasher _passwordHasher; 
    private readonly IConfiguration _config;
    private readonly IMenuService _menuService;
    public AuthenticationService(
        SQLGenerateHelper sqlHelper,
        IHttpContextAccessor httpContextAccessor,
        IPasswordHasher passwordHasher,
        IConfiguration config,
        IMenuService menuService)
    {
        _sqlHelper = sqlHelper;
        _httpContextAccessor = httpContextAccessor;
        _passwordHasher = passwordHasher;
        _config = config; 
        _menuService = menuService;
    }
    
    public async Task<Result<LoginResponseViewModel>> H5LoginAsync(string account, string password, CancellationToken ct = default)
    {
        var where = new WhereBuilder<UserAccount>()
            .AndEq(x => x.Account, account);

        var user = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);

        if (user == null)
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "�b���αK�X���~");
        
        bool isValid = _passwordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);

        if (!isValid)
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "�b���αK�X���~");
        
        string userLv = user.LV?.ToString() ?? "0";

        var claims = new List<Claim>
        {
            new Claim(AppClaimTypes.Account, user.Account),
            new Claim(AppClaimTypes.UserId, user.Id.ToString()),
            new Claim(AppClaimTypes.UserLv, userLv)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
       
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, 
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(_config.GetValue<int>("AuthSettings:ExpireTimeSpanMinutes")),
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