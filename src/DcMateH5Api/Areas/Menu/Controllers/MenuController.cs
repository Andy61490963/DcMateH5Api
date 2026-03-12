using DCMATEH5API.Areas.Menu.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DcMateH5.Abstractions.Menu;

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
            // 1. 動態讀取登入者資訊
            var currentUser = User.Identity?.Name ?? "Guest";

            // 2. 讀取登入時自定義存入的 "UserLV" (假設權限控制需要)
            var currentLvStr = User.FindFirst("UserLV")?.Value ?? "0";
            int.TryParse(currentLvStr, out int currentLv);

            // 3. ⭐ 關鍵修改：直接向 Service 要「已經處理好」的 MenuResponse
            // Service 內部現在會處理：
            // - 從資料庫 NULL 啟動樹狀結構
            // - 過濾 Root 節點以免首頁出現重複入口
            // - 遞迴填充 MenuList 並計算正確的 BackUrl
            var finalResult = await _menuService.GetFullMenuByLvAsync(currentLv);

            // 4. 回傳統一格式
            return Ok(new MenuResult
            {
                Success = true,
                Data = finalResult,
                Message = "查詢成功",
                User = currentUser,
                LV = currentLvStr
            });
        }
       
    }
}