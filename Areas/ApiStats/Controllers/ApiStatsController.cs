using System.Text.Json.Serialization;
using DcMateH5Api.DbExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using DcMateH5Api.Helper;
using DcMateH5Api.Models;

namespace DcMateH5Api.Areas.ApiStats.Controllers
{
    /// <summary>
    /// 提供 API 狀態測試、清單與統計資訊（JSON）
    /// </summary>
    [Area("ApiStats")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.ApiStatus)]
    [Route("[area]/[controller]")]
    public class ApiStatusController : ControllerBase
    {
        private const int DefaultPageSize = 200;
        private const int MaxPageSize = 1000;

        private readonly IApiDescriptionGroupCollectionProvider _provider;

        public ApiStatusController(IApiDescriptionGroupCollectionProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// API 存活與基本資訊
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            var data = new HealthResponse
            {
                Instance = Environment.MachineName,
                Time = DateTime.Now
            };

            return Ok(Result<HealthResponse>.Ok(data));
        }
        
        /// <summary>
        /// 測試 LogService 使用的資料庫連線是否正常
        /// </summary>
        /// <remarks>
        /// 功能說明：
        /// 1. 由 DI 取得 SQL 連線工廠（內部已帶好連線字串）
        /// 2. 嘗試開啟資料庫連線，並計算連線所需時間
        /// 3. 執行一筆最簡單的 SQL（SELECT GETDATE()）
        ///    用來確認「真的連得上資料庫、而且可以執行指令」
        /// 4. 回傳目前連線的 DB 位置與資料庫名稱，方便確認是否指到正確環境
        /// </remarks>
        [HttpGet("db-health-connection")]
        public async Task<IActionResult> DbHealthConnection(
            [FromServices] ISqlConnectionFactory factory,
            CancellationToken ct)
        {
            var conn = factory.Create();
            await using (conn)
            {
                await conn.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT GETDATE()1"; // 故意錯
                await cmd.ExecuteScalarAsync(ct);
            }

            return Ok(Result<string>.Ok("OK"));
        }
        
        /// <summary>
        /// 回傳所有可公開的 API 清單與總數
        /// </summary>
        /// <remarks>
        /// Query 參數說明:
        /// - keyword=xxx：模糊搜尋（Path/Controller/Action）
        /// - method=GET：只看某 HTTP Method
        /// - controller=Form：只看某 Controller
        /// - page=1，pageSize=200：分頁
        /// </remarks>
        [HttpGet]
        public IActionResult Get(
            [FromQuery] string? keyword,
            [FromQuery] string? method,
            [FromQuery] string? controller,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = DefaultPageSize)
        {
            // 1) 先把所有 API 描述轉成扁平清單（方便後續做篩選/排序/分頁）
            var allItems = BuildAllApiItems();

            // 2) 套用篩選條件（method/controller/keyword）
            var filtered = ApplyFilters(allItems, keyword, method, controller);

            // 3) 依固定規則排序（Path -> Method -> Controller -> Action）
            var sorted = ApplySort(filtered);

            // 4) 分頁參數防呆（避免 0 或爆量）
            NormalizePaging(ref page, ref pageSize);

            // 5) 計算總筆數 + 取出當頁資料
            var total = sorted.Count();
            var pageItems = sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 6) 組合回傳 DTO（扁平清單 + 依 Controller 群組）
            var result = new ApiStatsResponse
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = pageItems,
                GroupByController = BuildGroupByController(pageItems)
            };

            return Ok(Result<ApiStatsResponse>.Ok(result));
        }

        #region Private helpers

        /// <summary>
        /// 從 ApiExplorer 取得所有 API 描述，並轉成 ApiItem 清單
        /// 
        /// 補充：
        /// - ApiExplorer 可能會針對同一個 action 產生重複描述（例如不同格式）
        /// - 所以這裡會 Distinct 去重
        /// </summary>
        private List<ApiItem> BuildAllApiItems()
        {
            var items = _provider.ApiDescriptionGroups.Items
                .SelectMany(g => g.Items)
                .Select(d => new ApiItem
                {
                    Method = ((d.HttpMethod ?? "ANY").Trim()).ToUpperInvariant(),
                    Path = BuildNormalizedPath(d.RelativePath),
                    Controller = GetRouteValue(d, "controller"),
                    Action = GetRouteValue(d, "action")
                })
                .Distinct()
                .ToList();

            return items;
        }

        /// <summary>
        /// 統一路徑格式：確保以 '/' 開頭，並移除多餘 '/'
        /// </summary>
        private static string BuildNormalizedPath(string? relativePath)
        {
            var path = relativePath ?? string.Empty;
            path = path.TrimStart('/');
            return "/" + path;
        }

        /// <summary>
        /// 安全取得 route value（controller/action）
        /// </summary>
        private static string GetRouteValue(ApiDescription d, string key)
        {
            if (d.ActionDescriptor.RouteValues.TryGetValue(key, out var value) && value != null)
            {
                return value;
            }
            return string.Empty;
        }

        /// <summary>
        /// 套用篩選條件：
        /// - method：完全比對
        /// - controller：大小寫不敏感完全比對
        /// - keyword：包含比對（Path/Controller/Action）
        /// </summary>
        private static IEnumerable<ApiItem> ApplyFilters(
            IEnumerable<ApiItem> source,
            string? keyword,
            string? method,
            string? controller)
        {
            var query = source;

            if (!string.IsNullOrWhiteSpace(method))
            {
                var m = method.Trim().ToUpperInvariant();
                query = query.Where(x => x.Method == m);
            }

            if (!string.IsNullOrWhiteSpace(controller))
            {
                var c = controller.Trim();
                query = query.Where(x => x.Controller.Equals(c, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                query = query.Where(x =>
                    x.Path.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    x.Controller.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    x.Action.Contains(k, StringComparison.OrdinalIgnoreCase));
            }

            return query;
        }

        /// <summary>
        /// 固定排序規則：讓輸出穩定、可預期（方便前端顯示與比對）
        /// </summary>
        private static IOrderedEnumerable<ApiItem> ApplySort(IEnumerable<ApiItem> source)
        {
            return source
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Method, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Controller, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Action, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 分頁參數防呆：
        /// - page <= 0 -> 1
        /// - pageSize <= 0 或 > Max -> Default
        /// </summary>
        private static void NormalizePaging(ref int page, ref int pageSize)
        {
            if (page <= 0) page = 1;

            if (pageSize <= 0 || pageSize > MaxPageSize)
            {
                pageSize = DefaultPageSize;
            }
        }

        /// <summary>
        /// 依 Controller 分群，方便前端做樹狀/群組 UI
        /// 注意：這裡是以「當頁 items」做群組（不是全量）
        /// </summary>
        private static Dictionary<string, List<ApiItem>> BuildGroupByController(List<ApiItem> items)
        {
            return items
                .GroupBy(i => i.Controller)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(x => x.Method, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(x => x.Action, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region DTOs

        /// <summary>
        /// API 清單項目（用於扁平清單與群組清單）
        /// </summary>
        public sealed class ApiItem : IEquatable<ApiItem>
        {
            [JsonPropertyName("method")] public string Method { get; set; } = "";
            [JsonPropertyName("path")] public string Path { get; set; } = "";
            [JsonPropertyName("controller")] public string Controller { get; set; } = "";
            [JsonPropertyName("action")] public string Action { get; set; } = "";

            /// <summary>
            /// Distinct 用來去重時需要「值相等」
            /// 我們用 Method+Path+Controller+Action 判定同一個 API
            /// </summary>
            public bool Equals(ApiItem? other)
            {
                if (other is null) return false;

                return string.Equals(Method, other.Method, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(Controller, other.Controller, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(Action, other.Action, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object? obj) => obj is ApiItem o && Equals(o);

            public override int GetHashCode()
            {
                // HashCode 是 .NET 內建工具，用於快速判斷集合元素（Distinct/Dictionary）
                // 這裡用大小寫不敏感，避免 GET/get 被當不同值
                var hc = new HashCode();
                hc.Add(Method, StringComparer.OrdinalIgnoreCase);
                hc.Add(Path, StringComparer.OrdinalIgnoreCase);
                hc.Add(Controller, StringComparer.OrdinalIgnoreCase);
                hc.Add(Action, StringComparer.OrdinalIgnoreCase);
                return hc.ToHashCode();
            }
        }

        /// <summary>
        /// API 統計回應格式
        /// </summary>
        public sealed class ApiStatsResponse
        {
            [JsonPropertyName("total")] public int Total { get; set; }
            [JsonPropertyName("page")] public int Page { get; set; }
            [JsonPropertyName("pageSize")] public int PageSize { get; set; }

            /// <summary>
            /// 扁平清單：適合表格顯示或前端自行加工
            /// </summary>
            [JsonPropertyName("items")] public List<ApiItem> Items { get; set; } = new();

            /// <summary>
            /// 依 Controller 群組：適合做左側樹狀/群組清單 UI
            /// </summary>
            [JsonPropertyName("groupByController")] public Dictionary<string, List<ApiItem>> GroupByController { get; set; } = new();
        }
        
        public sealed class HealthResponse
        {
            public string Instance { get; set; } = string.Empty;
            public DateTime Time { get; set; }
        }
        #endregion
    }
}
