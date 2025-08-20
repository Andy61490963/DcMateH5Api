using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DcMateH5Api.Services.Cache // 命名空間：放置快取相關服務
{
    /// <summary>使用 Redis 實作的快取服務</summary> // 透過 IDistributedCache 與 Redis 溝通
    public class RedisCacheService : ICacheService // Redis 快取服務實作
    {
        private readonly IDistributedCache _cache; // 注入的分散式快取介面
        private readonly ILogger<RedisCacheService> _logger; // 注入的日誌記錄器
        private readonly TimeSpan _defaultTtl; // 預設快取存活時間

        public RedisCacheService(IDistributedCache cache, IOptions<CacheOptions> options, ILogger<RedisCacheService> logger) // 建構式注入
        {
            _cache = cache; // 指派分散式快取實例
            _logger = logger; // 指派日誌記錄器
            _defaultTtl = TimeSpan.FromMinutes(options.Value.DefaultTtlMinutes); // 由設定檔取得預設 TTL
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) // 取得快取資料
        {
            var data = await _cache.GetAsync(key, ct); // 從 Redis 以鍵取得原始位元資料
            if (data == null) return default; // 若無資料則回傳預設值
            return JsonSerializer.Deserialize<T>(data); // 反序列化 JSON 成為物件
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) // 寫入快取資料
        {
            var options = new DistributedCacheEntryOptions // 建立快取設定物件
            {
                AbsoluteExpirationRelativeToNow = ttl ?? _defaultTtl // 設定相對到期時間，使用自訂 TTL 或預設值
            };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value); // 將物件序列化為 JSON 位元陣列
            await _cache.SetAsync(key, bytes, options, ct); // 寫入 Redis 並套用設定
        }

        public Task RemoveAsync(string key, CancellationToken ct = default) // 移除快取資料
        {
            return _cache.RemoveAsync(key, ct); // 直接呼叫 Redis 的移除方法
        }
    } // 類別結尾
} // 命名空間結尾
