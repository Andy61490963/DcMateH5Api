using ClassLibrary;
// 確保引用介面命名空間
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.ViewModels;
using DcMateH5Api.Models;
using DcMateH5Api.SqlHelper;
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

    public AuthenticationService(
        SQLGenerateHelper sqlHelper,
        IHttpContextAccessor httpContextAccessor,
        IPasswordHasher passwordHasher) // 2. 建構子注入
    {
        _sqlHelper = sqlHelper;
        _httpContextAccessor = httpContextAccessor;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// H5 專用登入：使用 Cookie 存儲狀態
    /// </summary>
    public async Task<Result<LoginResponseViewModel>> H5LoginAsync(string account, string password, CancellationToken ct = default)
    {
        // 1. 查詢資料庫：只根據帳號查詢
        var where = new WhereBuilder<UserAccount>()
            .AndEq(x => x.Account, account);

        var user = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);

        // 2. 驗證帳號是否存在
        if (user == null)
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "帳號或密碼錯誤");

        // 3. 密碼驗證：呼叫 PasswordHasher 執行 加解密邏輯
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

        // 4. 寫入 Cookie (H5 專用)
        await _httpContextAccessor.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));

        return Result<LoginResponseViewModel>.Ok(new LoginResponseViewModel
        {
            User = user.Account,
            LV = userLv,
            Sid = user.Id.ToString(),
            Token = "COOKIE_AUTH_SUCCESS"
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
}