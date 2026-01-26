using System.Text.Json;
using DcMateH5Api.Services.Cache.Redis.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DcMateH5Api.Services.Cache.Redis.Services
{
    /// <summary>使用 Redis 實作的快取服務</summary> // 透過 IDistributedCache 與 Redis 溝通
    public class RedisCacheService : ICacheService 
    {
        private readonly IDistributedCache _cache; // 注入的分散式快取介面
        private readonly ILogger<RedisCacheService> _logger; // 注入的日誌記錄器
        private readonly TimeSpan _defaultTtl; // 預設快取存活時間
        private readonly bool _enabled; // 是否開啟 Redis 快取

        public RedisCacheService(IDistributedCache cache, IOptions<CacheOptions> options, ILogger<RedisCacheService> logger) 
        {
            _cache = cache; 
            _logger = logger; 
            _defaultTtl = TimeSpan.FromMinutes(options.Value.DefaultTtlMinutes); 
            _enabled = options.Value.Enabled;
        }

        /// <summary>
        /// 取得快取資料
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ct"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            if (!_enabled) return default;

            try
            {
                var data = await _cache.GetAsync(key, ct);
                if (data == null || data.Length == 0) return default;

                return JsonSerializer.Deserialize<T>(data);
            }
            catch (Exception ex)
            {
                // 最簡策略：快取失敗 → 當作沒有快取
                _logger.LogWarning(ex, "Cache Get failed. Key={Key}", key);
                return default;
            }
        }

        /// <summary>
        /// 寫入快取資料
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="ttl"></param>
        /// <param name="ct"></param>
        /// <typeparam name="T"></typeparam>
        public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
        {
            if (!_enabled) return;

            try
            {
                var entryOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl ?? _defaultTtl
                };

                var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
                await _cache.SetAsync(key, bytes, entryOptions, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache Set failed. Key={Key}", key);
            }
        }

        /// <summary>
        /// 移除快取資料
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task RemoveAsync(string key, CancellationToken ct = default)
        {
            if (!_enabled) return;

            try
            {
                await _cache.RemoveAsync(key, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache Remove failed. Key={Key}", key);
            }
        }
    } 
}
