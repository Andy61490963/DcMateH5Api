using DcMateH5Api.Areas.Security.Models;

namespace DcMateH5Api.Areas.Security.ViewModels
{
    /// <summary>
    /// 登入結果回傳給前端的內容。
    /// </summary>
    public class LoginResponseViewModel(UserAccount user, TokenResult token)
    {
        /// <summary>
        /// 使用者完整資訊。
        /// </summary>
        public UserAccount User { get; set; } = user;
        
        public TokenResult Token { get; set; } = token;
    }
}
