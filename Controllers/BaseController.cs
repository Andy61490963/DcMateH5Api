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
        // -------- CurrentUser：強型別取用（未登入時 IsAuthenticated=false、Id=Guid.Empty） --------
        protected CurrentUserSnapshot CurrentUser => CurrentUserSnapshot.From(User);
    }

    /// <summary>
    /// 以 Claims 建立的當前使用者快照（強型別；避免魔法字串）。
    /// </summary>
    public sealed class CurrentUserSnapshot
    {
        public Guid Id { get; private init; }
        public bool IsAuthenticated { get; private init; }

        public static CurrentUserSnapshot From(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return new CurrentUserSnapshot { IsAuthenticated = false, Id = Guid.Empty };
            }

            var id = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            Guid.TryParse(id, out var userId);

            return new CurrentUserSnapshot
            {
                Id = userId,
                IsAuthenticated = userId != Guid.Empty
            };
        }
    }
}
