using DCMATEH5API.Areas.Menu.Models;
using DCMATEH5API.Areas.Menu.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DCMATEH5API.Areas.Menu.Controllers
{
    [Area("Menu")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class MenuController : ControllerBase
    {
        private readonly IMenuService _menuService;
        public MenuController(IMenuService menuService) { _menuService = menuService; }

        [HttpGet("GetMenuTree")]
        public async Task<IActionResult> GetNavigation()
        {
            string currentUserId = "SystemAdmin";
            var tree = await _menuService.GetMenuTreeAsync(currentUserId);

            var finalResult = new MenuResponse();

            // 預設首頁入口
            finalResult.Pages["index.html"] = new PageFolderViewModel
            {
                Title = "首頁",
                Url = "index.html",
                ImgIcon = "", // 首頁本身通常沒圖標，或可給預設值
                Tiles = tree.Select((node, index) => new TileViewModel
                {
                    Sid = node.Id,
                    Title = node.Title,
                    Url = node.Url,
                    Property = node.SourceType == "PAGE" ? "Page" : "MENU",
                    Lv = node.Lv,
                    Seq = node.SortOrder,
                    Pos = index + 1,
                    ImgIcon = node.ImgIcon,
                    Parameter = node.Parameter,
                }).ToList()
            };

            // 遞迴將樹狀平鋪到 Dictionary 中
            FillPagesDictionary(tree, finalResult.Pages);

            return Ok(new MenuResult { Success = true, Data = finalResult, Message = "查詢成功" });
        }

        private void FillPagesDictionary(List<MenuNavigationViewModel> nodes, Dictionary<string, PageFolderViewModel> pages, string backUrl = "index.html")
        {
            foreach (var node in nodes)
            {
                var key = node.Url?.Trim();
                if (string.IsNullOrEmpty(key) || node.Children.Count == 0) continue;

                if (!pages.ContainsKey(key))
                {
                    pages[key] = new PageFolderViewModel
                    {
                        Sid = node.Id,
                        Title = node.Title,
                        Url = CombineUrl(node.Url, node.Parameter),
                        Lv = node.Lv,
                        BackUrl = backUrl,
                        ModuleName = node.Title,
                        TypeGroup = node.Title,
                        ImgIcon = node.ImgIcon,
                        Desc = node.Desc,
                        Parameter = node.Parameter,
                        Tiles = node.Children.Select((child, index) => new TileViewModel
                        {
                            Sid = child.Id,
                            Title = child.Title,
                            Url = CombineUrl(child.Url, child.Parameter),
                            Property = child.SourceType == "PAGE" ? "Page" : "MENU",
                            Lv = child.Lv,
                            Seq = child.SortOrder,
                            Pos = index + 1,
                            ImgIcon = child.ImgIcon,
                            Desc = child.Desc,
                            Parameter = child.Parameter
                        }).ToList()
                    };
                }
                FillPagesDictionary(node.Children, pages, key);
            }
        }

        private string CombineUrl(string? url, string? parameter)
        {
            var cleanUrl = url?.Trim() ?? "";
            if (string.IsNullOrEmpty(parameter)) return cleanUrl;
            return cleanUrl.Contains("?") ? $"{cleanUrl}&{parameter}" : $"{cleanUrl}?{parameter}";
        }
    }
}