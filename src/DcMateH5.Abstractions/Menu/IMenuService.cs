using DcMateH5.Abstractions.Menu.Models;

namespace DcMateH5.Abstractions.Menu;

public interface IMenuService
{
    /// <summary>
    /// 取得 legacy AuthInfo 格式的選單與頁面資料
    /// </summary>
    /// <param name="lv">頁面層級</param>
    /// <param name="userId">使用者識別</param>
    /// <returns>Legacy AuthInfo</returns>
    Task<AuthInfo> GetFullMenuByLvAsync(string lv, Guid userId);
}
