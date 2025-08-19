using DynamicForm.Areas.Security.Models;

namespace DynamicForm.Areas.Security.Interfaces
{
    /// <summary>
    /// 產生 JWT Token 的介面。
    /// </summary>
    public interface ITokenGenerator
    {
        /// <summary>
        /// 為指定使用者產生 Token 結果。
        /// </summary>
        /// <param name="user">使用者資料。</param>
        /// <returns>Token 結果。</returns>
        TokenResult GenerateToken(UserAccount user);
    }
}
