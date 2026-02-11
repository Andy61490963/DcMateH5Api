using System.Security.Claims;
using DcMateH5Api.Models;
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
    /// 快速判斷：
    /// 1. 使用者是否已登入
    /// 2. 使用者的唯一識別碼（UserId）
    /// </summary>
    public sealed class CurrentUserSnapshot
    {
        /// <summary>
        /// 使用者帳號
        /// 若未登入，則為 Guid.Empty
        /// </summary>
        public string Account { get; private init; } = string.Empty;
        
        /// <summary>
        /// 使用者的唯一識別碼
        /// 若未登入，則為 Guid.Empty
        /// </summary>
        public Guid Id { get; private init; }
        
        /// <summary>
        /// 使用者的唯一識別碼
        /// 若未登入，則為 Guid.Empty
        /// </summary>
        public string Lv { get; private init; } = string.Empty;

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
            if (user?.Identity?.IsAuthenticated != true)
            {
                return new CurrentUserSnapshot { IsAuthenticated = false, Id = Guid.Empty };
            }

            var account = user.FindFirst(AppClaimTypes.Account)?.Value;
            var id = user.FindFirst(AppClaimTypes.UserId)?.Value;
            var lv = user.FindFirst(AppClaimTypes.UserLv)?.Value;
            Guid.TryParse(id, out var userId);

            return new CurrentUserSnapshot
            {
                Account = account,
                Id = userId,
                Lv = lv,
                IsAuthenticated = userId != Guid.Empty
            };
        }

    }
}
