using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.SqlHelper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace DcMateH5Api.Areas.Security.Filters
{
    /// <summary>
    /// 併發登入檢查 Filter（Concurrent Login Check）
    /// 
    /// 用途：
    /// - 防止同一帳號多裝置 / 多瀏覽器同時使用同一 Session
    /// - 驗證目前 Cookie 中的 LoginLogSid 是否仍為有效登入紀錄
    /// - 檢查是否已被登出或超過閒置時間（Timeout）
    /// 
    /// 行為說明：
    /// - 若 Session 無效 / 已登出 / Timeout → 強制 SignOut + 回傳 401
    /// - 若仍有效 → 更新 LAST_ACTIVE_TIME，允許請求繼續
    /// </summary>
    public class ConcurrentLoginCheckFilter : Attribute, IAsyncActionFilter
    {
        private readonly SQLGenerateHelper _sqlHelper;
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// 透過 DI 注入必要服務
        /// </summary>
        public ConcurrentLoginCheckFilter(
            SQLGenerateHelper sqlHelper,
            IConfiguration config,
            IHttpContextAccessor httpContextAccessor)
        {
            _sqlHelper = sqlHelper;
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Action 執行前的非同步攔截邏輯
        /// </summary>
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            // 取得目前 HttpContext 的使用者身分（來自 Cookie Authentication）
            var user = context.HttpContext.User;

            // 只處理「已通過驗證」的請求
            if (user.Identity?.IsAuthenticated == true)
            {
                // 從 Claims 中取得登入紀錄 SID（登入時寫入 Claim）
                var loginLogSidStr = user.FindFirst("LoginLogSid")?.Value;

                // Claim 存在且為合法 GUID 才繼續檢查
                if (Guid.TryParse(loginLogSidStr, out var loginLogSid))
                {
                    // 建立查詢條件：依 LoginLogSid 找對應登入紀錄
                    var where = new WhereBuilder<UserLoginLogDto>()
                        .AndEq(x => x.ADM_USER_LOGIN_LOG_SID, loginLogSid);

                    // 查詢登入紀錄
                    var log = await _sqlHelper
                        .SelectFirstOrDefaultAsync(where, context.HttpContext.RequestAborted);

                    if (log == null)
                    {
                        // 情境：
                        // - Cookie 還在，但 DB 中登入紀錄已被清掉
                        // - 可能是後台強制登出或資料異常
                        // 
                        // 處理：
                        // - 強制登出
                        // - 回傳 401（Invalid Session）
                        await context.HttpContext.SignOutAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);

                        context.Result = new UnauthorizedObjectResult("Invalid Session");
                        return;
                    }

                    if (log.LOGOUT_TIME.HasValue)
                    {
                        // 情境：
                        // - 該 Session 已被登出（例如其他裝置重新登入）
                        // 
                        // 處理：
                        // - 強制登出目前 Cookie
                        // - 回傳 401
                        await context.HttpContext.SignOutAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);

                        context.Result = new UnauthorizedObjectResult(
                            "Session Expired (Logged Out)");
                        return;
                    }

                    // 從設定檔讀取 Session 閒置逾時分鐘數
                    int expireMinutes =
                        _config.GetValue<int>("AuthSettings:ExpireTimeSpanMinutes");

                    // 檢查是否超過最後活動時間 + Timeout
                    if (DateTime.Now >
                        log.LAST_ACTIVE_TIME.AddMinutes(expireMinutes))
                    {
                        // 情境：
                        // - 使用者長時間未操作，Session 過期
                        // 
                        // 處理：
                        // - 補寫 LOGOUT_TIME（維持登入紀錄完整性）
                        // - 強制登出
                        // - 回傳 401
                        await _sqlHelper
                            .UpdateById<UserLoginLogDto>(loginLogSid)
                            .Set(x => x.LOGOUT_TIME, DateTime.Now)
                            .ExecuteAsync(context.HttpContext.RequestAborted);

                        await context.HttpContext.SignOutAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);

                        context.Result = new UnauthorizedObjectResult("Session Timeout");
                        return;
                    }

                    // Session 仍有效：
                    // - 更新最後活動時間（Sliding Expiration）
                    await _sqlHelper
                        .UpdateById<UserLoginLogDto>(loginLogSid)
                        .Set(x => x.LAST_ACTIVE_TIME, DateTime.Now)
                        .ExecuteAsync(context.HttpContext.RequestAborted);
                }
            }

            // 所有檢查通過，放行請求進入 Controller Action
            await next();
        }
    }
}
