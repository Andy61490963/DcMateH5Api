using DcMateH5Api.Areas.Test.Controllers;
using DcMateH5Api.Areas.Test.Models;

namespace DcMateH5Api.Areas.Test.Interfaces
{
    public interface ITestService
    {
        // 建立（一律傳雜湊與鹽；先不做加密邏輯）
        Task<Guid> CreateUserAsync(TestController.CreateUserInput entity, CancellationToken ct = default);

        // 讀取
        Task<UserAccount?> GetUserByIdAsync(Guid id, CancellationToken ct = default);

        // 查詢（可用 account/name 模糊搜尋）
        Task<List<UserAccount>> SearchUsersAsync(string? account, string? name, CancellationToken ct = default);

        // 更新（可選擇 includeNulls：true=會把 null 寫回，false=忽略 null）
        Task<int> UpdateUserAsync(
            TestController.UpdateUserInput entity,
            CancellationToken ct = default);

        // 刪除
        Task<int> DeleteUserAsync(Guid id, CancellationToken ct = default);

        // 建議的 async Demo
        Task<List<UserAccount?>> GetTestAsync(CancellationToken ct = default);
    }
}