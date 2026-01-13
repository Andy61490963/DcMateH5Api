using DCMATEH5API.Areas.Menu.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DCMATEH5API.Areas.Menu.Services
{
    public interface IMenuService
    {
        // 取得原始樹狀資料的方法
        Task<List<MenuNavigationViewModel>> GetMenuTreeAsync(string userId);
    }
}