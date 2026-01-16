using DCMATEH5API.Areas.Menu.Models;
using DCMATEH5API.Areas.Menu.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization; // 確保加入此命名空間
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DCMATEH5API.Areas.Menu.Controllers
{
    [Area("Menu")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    [Authorize] // 加上授權，確保只有登入者能撈選單
    public class MenuController : ControllerBase
    {
        private readonly IMenuService _menuService;
        public MenuController(IMenuService menuService) { _menuService = menuService; }

        [HttpGet("GetMenuTree")]
        public async Task<IActionResult> GetNavigation()
        {
            // --- 修正：動態讀取登入者資訊 ---
            // 讀取登入時存入 ClaimTypes.Name 的帳號
            var currentUser = User.Identity?.Name ?? "Guest";

            // 讀取登入時自定義存入的 "UserLV"
            var currentLv = User.FindFirst("UserLV")?.Value ?? "0";

            // 使用動態帳號去撈取該人的選單樹
            var tree = await _menuService.GetMenuTreeAsync(currentUser);

            var finalResult = new MenuResponse();

            // 預設首頁入口
            finalResult.Pages["index.html"] = new PageFolderViewModel
            {
                Title = "首頁",
                Url = "index.html",
                ImgIcon = "",
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

            // --- 修正：回傳真實的 user 與 LV ---
            return Ok(new MenuResult
            {
                Success = true,
                Data = finalResult,
                Message = "查詢成功",
                User = currentUser, // 回傳登入帳號
                LV = currentLv      // 回傳資料庫撈出的 LV
            });
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
                        Url = node.Url,
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
                            Url = child.Url,
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
    }
}