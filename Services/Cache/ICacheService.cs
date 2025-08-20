using System;
using System.Threading;
using System.Threading.Tasks;

namespace DcMateH5Api.Services.Cache // 命名空間：放置快取相關服務
{
    /// <summary>定義快取服務介面</summary> // 所有快取操作必須遵循的介面
    public interface ICacheService // 快取服務介面
    {
        Task<T?> GetAsync<T>(string key, CancellationToken ct = default); // 非同步取得快取資料
        Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default); // 非同步寫入快取資料並可設定存活時間
        Task RemoveAsync(string key, CancellationToken ct = default); // 非同步移除指定快取
    } // 介面結尾
} // 命名空間結尾
