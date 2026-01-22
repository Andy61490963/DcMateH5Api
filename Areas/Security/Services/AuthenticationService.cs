using ClassLibrary;
// �T�O�ޥΤ����R�W�Ŷ�
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

/// <summary>
/// ���ҪA�ȹ�@
/// </summary>
// �ץ����I�G�����ϥΥ��W�~�ӡA�ѨM CS0104 �ҽk�ѦҰ��D
public class AuthenticationService : Interfaces.IAuthenticationService
{
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPasswordHasher _passwordHasher; // 1. �ŧi�[�K�B�z��
    private readonly IConfiguration _config; // 1. �ŧi�]�w�ɪA��
    private readonly IMenuService _menuService;
    public AuthenticationService(
        SQLGenerateHelper sqlHelper,
        IHttpContextAccessor httpContextAccessor,
        IPasswordHasher passwordHasher,
        IConfiguration config,
        IMenuService menuService) // <--- ����ץ��G�[�J�o��`�J
    {
        _sqlHelper = sqlHelper;
        _httpContextAccessor = httpContextAccessor;
        _passwordHasher = passwordHasher;
        _config = config; 
        _menuService = menuService;
    }

    /// <summary>
    /// H5 �M�εn�J�G�ϥ� Cookie �s�x���A
    /// </summary>
    public async Task<Result<LoginResponseViewModel>> H5LoginAsync(string account, string password, CancellationToken ct = default)
    {
        //  �d�߸�Ʈw�G�u�ھڱb���d��
        var where = new WhereBuilder<UserAccount>()
            .AndEq(x => x.Account, account);

        var user = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);

        //  ���ұb���O�_�s�b
        if (user == null)
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "�b���αK�X���~");

        //  �K�X���ҡG�I�s PasswordHasher ���� �[�ѱK�޿�
        bool isValid = _passwordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);

        if (!isValid)
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.UserNotFound, "�b���αK�X���~");

        // --- ���Ҧ��\�A�ǳƼg�J Cookie ---
        string userLv = user.LV?.ToString() ?? "0";

        var claims = new List<Claim>
        {
            new (ClaimTypes.Name, user.Account),
            new ("UserId", user.Id.ToString()),
            new ("UserLV", userLv)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
       
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, // �֤ߧ�ʡG���[��
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(_config.GetValue<int>("AuthSettings:ExpireTimeSpanMinutes")),
        };

        // --- 1. ����n�J (�o�|���t�Φb Response Header ���� Cookie) ---
        await _httpContextAccessor.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        // --- 2. ����[�K�� Ticket ---
        var setCookieHeader = _httpContextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();
        // �u�� DcMateAuthTicket=... �������A�åh���W�١A�u�d�ª��_
        string encryptedTicket = string.Empty;
        if (!string.IsNullOrEmpty(setCookieHeader))
        {
            var ticketPart = setCookieHeader.Split(';')[0]; // ���o "DcMateAuthTicket=CfDJ8..."
            encryptedTicket = ExtractTokenFromResponse(); // �u�O�d�¶ýX���_
        }

        // --- 3. �p����X���Ĵ� (�P authProperties �P�B) ---
        // �N�L���ɶ��ഫ�� ISO �榡�r��A��K�e�� JS �ѪR
        string expiresFrom = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); // �{�b�ɶ�
        string expiresTo = authProperties.ExpiresUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "";

        // �I�s���A�� (���]�z�� IMenuService �^�Ǫ��N�O MenuResponse)
        var menuData = await _menuService.GetFullMenuByLvAsync(user.Account);

        // --- 4. �ʸ˨æ^�� ---
        return Result<LoginResponseViewModel>.Ok(new LoginResponseViewModel
        {
            User = user.Account,
            LV = user.LV?.ToString() ?? "0",
            Sid = user.Id.ToString(),
            // �^�ǥ[�K���_���e�ݦs localStorage
            Token = encryptedTicket,
            // �s�W�G�^�ǩ��X���Ĵ�
            ExpiresFrom = expiresFrom,
            ExpiresTo = expiresTo,
            // �^�Ǳz��W�᪺ MenuList �r��
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
        // 1. ���o�ثe HttpContext �����ϥΪ̨���
        var user = _httpContextAccessor.HttpContext.User;

        if (user == null || !user.Identity.IsAuthenticated)
        {
            // �ץ��I�G�[�W AuthenticationErrorCode
            return Result<bool>.Fail(AuthenticationErrorCode.UserNotFound, "�����w�L���εL��");
        }

        // 3. �q�{���� Cookie ��������T (UserId, UserLV ��)
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
            // ���g ExpiresUtc�A�����۰ʦY Program.cs �� 8 �p�ɳ]�w
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

        // �ץ��᪺�{���X�G�ϥβ{���� Unauthorized ����
        if (!refreshResult.IsSuccess)
        {
            return Result<LoginResponseViewModel>.Fail(AuthenticationErrorCode.Unauthorized, refreshResult.Message);
        }

        // 2. �Τ@�B�z Token ����
        var setCookieHeader = _httpContextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();
        string newTicket = "";
        if (setCookieHeader.Contains("DcMateAuthTicket="))
        {
            newTicket = ExtractTokenFromResponse();
        }

        // 3. �^�ǫʸ˫᪺���G
        return Result<LoginResponseViewModel>.Ok(new LoginResponseViewModel
        {
            Token = newTicket,
            ExpiresFrom = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ExpiresTo = DateTime.Now.AddMinutes(_config.GetValue<int>("AuthSettings:ExpireTimeSpanMinutes")).ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    // �s�W�o�Өp���u��A�M���t�d�q Response �������_
    private string ExtractTokenFromResponse()
    {
        var setCookieHeader = _httpContextAccessor.HttpContext.Response.Headers["Set-Cookie"].ToString();

        if (!string.IsNullOrEmpty(setCookieHeader) && setCookieHeader.Contains("DcMateAuthTicket="))
        {
            // ���o�Ĥ@�Ӥ��q�ò����W�ٳ���
            return setCookieHeader.Split(';')[0].Replace("DcMateAuthTicket=", "");
        }

        return string.Empty;
    }
}