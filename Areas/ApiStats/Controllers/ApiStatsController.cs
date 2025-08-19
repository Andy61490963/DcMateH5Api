using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using DynamicForm.Helper;

namespace DynamicForm.Areas.ApiStats.Controllers
{
    /// <summary>
    /// API 統計與目錄（JSON 版）
    /// 此段純 GPT 生成，方便參考而已，不需維護
    /// </summary>
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.ApiStats)]
    [Route("tools/apistats")]
    [Produces("application/json")]
    public class ApiStatsController : ControllerBase
    {
        private readonly IApiDescriptionGroupCollectionProvider _provider;

        public ApiStatsController(IApiDescriptionGroupCollectionProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// 回傳所有可公開的 API 清單與總數
        /// Query: ?keyword=xxx&method=GET&controller=Form&page=1&pageSize=200
        /// </summary>
        [HttpGet]
        public IActionResult Get(
            [FromQuery] string? keyword,
            [FromQuery] string? method,
            [FromQuery] string? controller,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 200)
        {
            // 取得所有 API 描述
            var all = _provider.ApiDescriptionGroups.Items
                .SelectMany(g => g.Items)
                .Select(d => new ApiItem
                {
                    Method = (d.HttpMethod ?? "ANY").ToUpperInvariant(),
                    Path = "/" + (d.RelativePath ?? string.Empty).TrimStart('/'),
                    Controller = d.ActionDescriptor.RouteValues.TryGetValue("controller", out var c) ? c ?? "" : "",
                    Action = d.ActionDescriptor.RouteValues.TryGetValue("action", out var a) ? a ?? "" : ""
                })
                // 去重（同一路徑/動詞可能在不同產出重複）
                .Distinct()
                .ToList();

            // 篩選
            IEnumerable<ApiItem> query = all;

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

            // 排序（Path -> Method -> Controller -> Action）
            query = query
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Method, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Controller, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Action, StringComparer.OrdinalIgnoreCase);

            // 分頁安全檢查
            page = page <= 0 ? 1 : page;
            pageSize = pageSize is <= 0 or > 1000 ? 200 : pageSize;

            var total = query.Count();
            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var result = new ApiStatsResponse
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = items,
                GroupByController = items
                    .GroupBy(i => i.Controller)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(x => x.Method, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(x => x.Action, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        StringComparer.OrdinalIgnoreCase)
            };

            return Ok(result);
        }

        // 輸出 DTO：清單項目
        public sealed class ApiItem : IEquatable<ApiItem>
        {
            [JsonPropertyName("method")] public string Method { get; set; } = "";
            [JsonPropertyName("path")] public string Path { get; set; } = "";
            [JsonPropertyName("controller")] public string Controller { get; set; } = "";
            [JsonPropertyName("action")] public string Action { get; set; } = "";

            // Distinct 需要值相等比較
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
                var hc = new HashCode();
                hc.Add(Method, StringComparer.OrdinalIgnoreCase);
                hc.Add(Path, StringComparer.OrdinalIgnoreCase);
                hc.Add(Controller, StringComparer.OrdinalIgnoreCase);
                hc.Add(Action, StringComparer.OrdinalIgnoreCase);
                return hc.ToHashCode();
            }
        }

        // 輸出 DTO：回應包
        public sealed class ApiStatsResponse
        {
            [JsonPropertyName("total")] public int Total { get; set; }
            [JsonPropertyName("page")] public int Page { get; set; }
            [JsonPropertyName("pageSize")] public int PageSize { get; set; }

            /// <summary>扁平清單，便於表格顯示或前端再加工</summary>
            [JsonPropertyName("items")] public List<ApiItem> Items { get; set; } = new();

            /// <summary>依 Controller 群組的字典（方便左側樹狀/群組顯示）</summary>
            [JsonPropertyName("groupByController")] public Dictionary<string, List<ApiItem>> GroupByController { get; set; } = new();
        }
    }
}
