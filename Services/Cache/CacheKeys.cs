using System;

namespace DcMateH5Api.Services.Cache
{
    /// <summary>
    /// 統一管理快取鍵命名，集中維護避免魔法字串。
    /// 透過具語意的方法建立鍵值，可降低輸入錯誤並提升可讀性。
    /// </summary>
    public static class CacheKeys
    {
        /// <summary>使用者選單快取鍵。</summary>
        public static string UserMenu(Guid userId) => $"user_menu:{userId}";

        /// <summary>使用者權限快取鍵。</summary>
        public static string UserPermission(Guid userId) => $"user_permissions:{userId}";

        /// <summary>使用者控制器權限快取鍵。</summary>
        public static string ControllerPermission(Guid userId, string area, string controller, int actionCode)
            => $"Permission:{userId}:{area}:{controller}:{actionCode}";
    }
}

