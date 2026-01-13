using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using DCMATEH5API.Areas.Menu.Models; // 引用你自己的 Models

namespace DCMATEH5API.Areas.Menu.Controllers
{
    [Area("Menu")]
    [Route("api/[area]/[controller]")]
    [ApiController] // 加入這個標籤，會自動處理模型驗證
    public class MenuController : ControllerBase // 改為繼承內建的 ControllerBase
    {
        [HttpGet]
        public IActionResult GetNavigation()
        {
            // 1. 取得資料 (目前模擬)
            var allItems = GetMockData(); 

            // 2. 組裝成樹狀結構
            var menuTree = BuildMenuTree(allItems);

            // 3. 使用你自己定義的回傳格式
            var result = new MenuResult
            {
                Success = true,
                Message = "Success",
                Data = menuTree
            };

            return Ok(result);
        }

        // --- 以下為樹狀組裝邏輯 (保持不變) ---

        private List<MenuNavigationViewModel> BuildMenuTree(List<MenuNavigationViewModel> source)
        {
            if (source == null) return new List<MenuNavigationViewModel>();

            var groups = source.Where(x => string.IsNullOrEmpty(x.ParentId))
                               .OrderBy(x => x.SortOrder)
                               .ToList();

            foreach (var item in groups)
            {
                item.Children = GetChildren(source, item.Id);
            }
            return groups;
        }

        private List<MenuNavigationViewModel> GetChildren(List<MenuNavigationViewModel> source, string parentId)
        {
            var children = source.Where(x => x.ParentId == parentId)
                                 .OrderBy(x => x.SortOrder)
                                 .ToList();

            foreach (var child in children)
            {
                var grandChildren = GetChildren(source, child.Id);
                child.Children = grandChildren ?? new List<MenuNavigationViewModel>();
            }
            return children;
        }

        private List<MenuNavigationViewModel> GetMockData()
        {
            return new List<MenuNavigationViewModel>
            {
                new MenuNavigationViewModel { Id = "1", ParentId = "0", Title = "系統配置", Translate = "MENU.SYSTEM_CONFIG", Type = "group", SortOrder = 1 },
                new MenuNavigationViewModel { Id = "2", ParentId = "1", Title = "使用者管理", Translate = "MENU.USER_MANAGEMENT", Type = "collapsable", SortOrder = 1 },
                new MenuNavigationViewModel { Id = "3", ParentId = "2", Title = "清單", Url = "/system/users", Type = "item", SortOrder = 1 }
            };
        }
    }
}