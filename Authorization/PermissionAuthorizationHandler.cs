using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using DynamicForm.Areas.Permission.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;

namespace DynamicForm.Authorization
{
    /// <summary>
    /// 依 Controller 範疇檢查某 action 的權限（例如 View/Edit/Delete）。
    /// </summary>
    public sealed class PermissionRequirementScopedToController : IAuthorizationRequirement
    {
        public int ActionCode { get; }
        public PermissionRequirementScopedToController(int actionCode) => ActionCode = actionCode;
    }

    public sealed class PermissionAuthorizationHandler
        : AuthorizationHandler<PermissionRequirementScopedToController>
    {
        private readonly IPermissionService _permissionService;
        private readonly IHttpContextAccessor _http;

        public PermissionAuthorizationHandler(IPermissionService permissionService, IHttpContextAccessor http)
        {
            _permissionService = permissionService;
            _http = http;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirementScopedToController requirement)
        {
            // 1) 驗證身份存在
            if (context.User?.Identity?.IsAuthenticated != true) return;

            // 2) 取 userId：先取 sub，失敗再取 NameIdentifier
            if (!TryGetUserId(context.User, out var userId)) return;

            // 3) 拿到目前路由的 Area / Controller（用 RouteValues 最穩定）
            var httpCtx = _http.HttpContext;
            var routeValues = httpCtx?.GetRouteData()?.Values;

            var area = (routeValues?["area"]?.ToString() ?? "").Trim();
            var controller = (routeValues?["controller"]?.ToString() ?? "").Trim();

            // 4) 向服務詢問是否擁有該 (Area, Controller, ActionCode) 的權限
            var ok = await _permissionService.UserHasControllerPermissionAsync(
                userId, area, controller, requirement.ActionCode);

            if (ok)
            {
                context.Succeed(requirement);
            }
            // 沒權限就什麼都不做，讓 Authorization 走失敗分支
        }

        /// <summary>
        /// 同時容錯 sub 與 NameIdentifier，並確保為 Guid。
        /// </summary>
        private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
        {
            userId = Guid.Empty;

            // 先試 sub（JwtRegisteredClaimNames.Sub）
            var id = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            // 映射情境（MapInboundClaims=true）時，sub 會被映成 NameIdentifier
            id ??= user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(id, out userId);
        }
    }
}
