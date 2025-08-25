using System.Threading.Tasks;
using DcMateH5Api.Areas.Security.ViewModels;

namespace DcMateH5Api.Areas.Security.Interfaces
{
    /// <summary>
    /// 提供使用者驗證功能的服務介面。
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// 驗證帳號密碼並產生 Token。
        /// </summary>
        /// <param name="account">使用者帳號。</param>
        /// <param name="password">使用者密碼。</param>
        /// <returns>登入結果，失敗則回傳 null。</returns>
        Task<LoginResponseViewModel?> AuthenticateAsync(string account, string password, CancellationToken ct = default);

        Task<int> RegisterAsync(RegisterRequestViewModel request, CancellationToken ct = default);
    }
}
