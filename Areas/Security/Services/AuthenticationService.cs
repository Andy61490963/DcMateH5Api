using ClassLibrary;
// 確保引用介面命名空間
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.ViewModels;
using DcMateH5Api.Models;
using DcMateH5Api.SqlHelper;
using DCMATEH5API.Areas.Menu.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace DcMateH5Api.Areas.Security.Services;

/// <summary>
/// 驗證服務實作
/// </summary>
// 修正重點：直接使用全名繼承，解決 CS0104 模糊參考問題
public class AuthenticationService : DcMateH5Api.Areas.Security.Interfaces.IAuthenticationService
{
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPasswordHasher _passwordHasher; // 1. 宣告加密處理器
    private readonly IConfiguration _config; // 1. 宣告設定檔服務
    private readonly IMenuService _menuService;
    public AuthenticationService(
        SQLGenerateHelper sqlHelper,
        IHttpContextAccessor httpContextAccessor,
        IPasswordHasher passwordHasher,
        IConfiguration config,
        IMenuService menuService) // <--- 關鍵修正：加入這行注入
    {
        _sqlHelper = sqlHelper;
        _httpContextAccessor = httpContextAccessor;
        _passwordHasher = passwordHasher;
        _config = config; 
        _menuService = menuService;
    }

    /// <summary>
    /// H5 專用登入：使用 Cookie 存儲狀態
    /// </summary>
    public async Task<Result<LoginResponseViewModel>> H5LoginAsync(string account, string password, CancellationToken ct = default)
    {
        //  查詢資料庫：只根據帳號查詢
        var where = new WhereBuilder<UserAccount>()
            .AndEq(x => x.Account, account);

        var user = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);

        //  驗證帳號是否存在
        if (user == null)
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "帳號或密碼錯誤");

        //  密碼驗證：呼叫 PasswordHasher 執行 加解密邏輯
        bool isValid = _passwordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);

        if (!isValid)
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "帳號或密碼錯誤");

        // --- 驗證成功，準備寫入 Cookie ---
        string userLv = user.LV?.ToString() ?? "0";

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Account),
            new Claim("UserId", user.Id.ToString()),
            new Claim("UserLV", userLv)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
       
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, // 核心改動：持久化
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(_config.GetValue<int>("AuthSettings:ExpireTimeSpanMinutes")),
        };

        // --- 1. 執行登入 (這會讓系統在 Response Header 產生 Cookie) ---
        await _httpContextAccessor.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        // --- 2. 抓取加密的 Ticket ---
        var setCookieHeader = _httpContextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();
        // 只取 DcMateAuthTicket=... 的部分，並去掉名稱，只留純金鑰
        string encryptedTicket = string.Empty;
        if (!string.IsNullOrEmpty(setCookieHeader))
        {
            var ticketPart = setCookieHeader.Split(';')[0]; // 取得 "DcMateAuthTicket=CfDJ8..."
            encryptedTicket = ExtractTokenFromResponse(); // 只保留純亂碼金鑰
        }

        // --- 3. 計算明碼有效期 (與 authProperties 同步) ---
        // 將過期時間轉換為 ISO 格式字串，方便前端 JS 解析
        string expiresFrom = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); // 現在時間
        string expiresTo = authProperties.ExpiresUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";

        // 呼叫選單服務 (假設您的 IMenuService 回傳的就是 MenuResponse)
        var menuData = await _menuService.GetFullMenuByLvAsync(user.LV ?? 0);

        // --- 4. 封裝並回傳 ---
        return Result<LoginResponseViewModel>.Ok(new LoginResponseViewModel
        {
            User = user.Account,
            LV = user.LV?.ToString() ?? "0",
            Sid = user.Id.ToString(),
            // 回傳加密金鑰給前端存 localStorage
            Token = encryptedTicket,
            // 新增：回傳明碼有效期
            ExpiresFrom = expiresFrom,
            ExpiresTo = expiresTo,
            // 回傳您改名後的 MenuList 字典
            Menus = menuData
        });
    }

    public async Task LogoutAsync()
    {
        await _httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public Task<Result<int>> RegisterAsync(RegisterRequestViewModel request, CancellationToken ct = default)
    {
        // 未來實作註冊時會用到 IdHelper.GenerateNumericId()
        throw new NotImplementedException();
    }

    public async Task<Result<LoginResponseViewModel>> AuthenticateAsync(string account, string password, CancellationToken ct = default)
    {
        return await H5LoginAsync(account, password, ct);
    }

    private async Task<Result<bool>> RefreshAuthCookieAsync()
    {
        // 1. 取得目前 HttpContext 中的使用者身分
        var user = _httpContextAccessor.HttpContext.User;

        if (user == null || !user.Identity.IsAuthenticated)
        {
            // 修正點：加上 AuthenticationErrorCode
            return Result<bool>.Fail(AuthenticationErrorCode.UserNotFound, "身分已過期或無效");
        }

        // 3. 從現有的 Cookie 中提取資訊 (UserId, UserLV 等)
        var userId = user.FindFirst("UserId")?.Value;
        var userLv = user.FindFirst("UserLV")?.Value;
        var account = user.Identity.Name;

        // 4. 重新封裝 Claims
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
            // 不寫 ExpiresUtc，讓它自動吃 Program.cs 的 8 小時設定
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

        // 修正後的程式碼：使用現有的 Unauthorized 成員
        if (!refreshResult.IsSuccess)
        {
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.Unauthorized, refreshResult.Message);
        }

        // 2. 統一處理 Token 提取
        var setCookieHeader = _httpContextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();
        string newTicket = "";
        if (setCookieHeader.Contains("DcMateAuthTicket="))
        {
            newTicket = ExtractTokenFromResponse();
        }

        // 3. 回傳封裝後的結果
        return Result<LoginResponseViewModel>.Ok(new LoginResponseViewModel
        {
            Token = newTicket,
            ExpiresFrom = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ExpiresTo = DateTime.Now.AddMinutes(_config.GetValue<int>("AuthSettings:ExpireTimeSpanMinutes")).ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    // 新增這個私有工具，專門負責從 Response 提取金鑰
    private string ExtractTokenFromResponse()
    {
        var setCookieHeader = _httpContextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();

        if (!string.IsNullOrEmpty(setCookieHeader) && setCookieHeader.Contains("DcMateAuthTicket="))
        {
            // 取得第一個分段並移除名稱部分
            return setCookieHeader.Split(';')[0].Replace("DcMateAuthTicket=", "");
        }

        return string.Empty;
    }
}