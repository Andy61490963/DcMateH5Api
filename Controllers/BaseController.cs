using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Controllers
{
    /// <summary>
    /// 提供：CurrentUser、授權便捷方法（by Policy / by Requirement / by Service）、Route 解析等。
    /// 與分散式架構、JWT 完全相容（無共享狀態；每請求讀 HttpContext.User）。
    /// </summary>
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        // -------- CurrentUser：（未登入時 IsAuthenticated=false、Id=Guid.Empty） --------
        protected CurrentUserSnapshot CurrentUser => CurrentUserSnapshot.From(User);
    }

    /// <summary>
    /// 代表「目前登入使用者」的簡化快照物件
    /// 
    /// 這個類別的目的不是存完整 User 資料，
    /// 而是快速判斷：
    /// 1. 使用者是否已登入
    /// 2. 使用者的唯一識別碼（UserId）
    /// </summary>
    public sealed class CurrentUserSnapshot
    {
        /// <summary>
        /// 使用者的唯一識別碼（來自 JWT 的 sub Claim）
        /// 若未登入，則為 Guid.Empty
        /// </summary>
        public Guid Id { get; private init; }

        /// <summary>
        /// 是否為已通過驗證的使用者
        /// </summary>
        public bool IsAuthenticated { get; private init; }

        /// <summary>
        /// 從 ClaimsPrincipal 建立目前使用者的快照
        /// </summary>
        /// <param name="user">目前 HTTP Context 中的使用者資訊</param>
        /// <returns>CurrentUserSnapshot</returns>
        public static CurrentUserSnapshot From(ClaimsPrincipal? user)
        {
            // 沒有使用者，或尚未通過驗證
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                return new CurrentUserSnapshot
                {
                    IsAuthenticated = false,
                    Id = Guid.Empty
                };
            }

            // 從 JWT 的 sub Claim 取得使用者 Id
            var idValue = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            // 嘗試轉成 Guid，失敗會得到 Guid.Empty (通常為未登入情況)
            Guid.TryParse(idValue, out var userId);

            return new CurrentUserSnapshot
            {
                Id = userId,
                IsAuthenticated = userId != Guid.Empty
            };
        }
    }
}
