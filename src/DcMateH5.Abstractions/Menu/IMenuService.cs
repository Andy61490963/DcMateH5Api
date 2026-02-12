using DCMATEH5API.Areas.Menu.Models;

namespace DcMateH5.Abstractions.Menu;

public interface IMenuService
{
    // 取得原始樹狀資料的方法
    Task<List<MenuNavigationViewModel>> GetMenuTreeAsync(string userId);

    Task<MenuResponse> GetFullMenuByLvAsync(int lv);
}
