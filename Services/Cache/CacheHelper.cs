namespace DcMateH5Api.Services.Cache
{
    /// <summary>
    /// 快取操作的擴充方法，將常見的快取讀寫封裝並統一使用 CacheKeys。
    /// 透過集中管理可降低重複程式碼並避免鍵值命名錯誤。
    /// </summary>
    public static class CacheHelper
    {
        /// <summary>取得使用者選單快取。</summary>
        public static Task<T?> GetUserMenuAsync<T>(this ICacheService cache, Guid userId, CancellationToken ct = default)
            => cache.GetAsync<T>(CacheKeys.UserMenu(userId), ct);

        /// <summary>設定使用者選單快取。</summary>
        public static Task SetUserMenuAsync<T>(this ICacheService cache, Guid userId, T value, CancellationToken ct = default)
            => cache.SetAsync(CacheKeys.UserMenu(userId), value, ct: ct);

        /// <summary>取得使用者特定控制器的權限快取。</summary>
        public static Task<bool?> GetControllerPermissionAsync(this ICacheService cache, Guid userId, string area, string controller, int actionCode, CancellationToken ct = default)
            => cache.GetAsync<bool?>(CacheKeys.ControllerPermission(userId, area, controller, actionCode), ct);

        /// <summary>設定使用者特定控制器的權限快取。</summary>
        public static Task SetControllerPermissionAsync(this ICacheService cache, Guid userId, string area, string controller, int actionCode, bool value, TimeSpan? ttl = null, CancellationToken ct = default)
            => cache.SetAsync(CacheKeys.ControllerPermission(userId, area, controller, actionCode), value, ttl, ct);

        /// <summary>移除與使用者相關的所有快取。</summary>
        public static Task RemoveUserCachesAsync(this ICacheService cache, Guid userId, CancellationToken ct = default)
            => Task.WhenAll(
                cache.RemoveAsync(CacheKeys.UserPermission(userId), ct),
                cache.RemoveAsync(CacheKeys.UserMenu(userId), ct));
    }
}

