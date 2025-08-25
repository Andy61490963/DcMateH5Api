using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Test.Controllers;
using DcMateH5Api.Areas.Test.Interfaces;
using DcMateH5Api.Areas.Test.Mappers;
using DcMateH5Api.Areas.Test.Models;
using DcMateH5Api.SqlHelper;

namespace DcMateH5Api.Areas.Test.Services
{
    /// <summary>
    /// TestService：示範架構中呼叫簡易版 CRUD。
    /// 設計想法：
    /// - 這層只負責「商業邏輯 + 參數檢查」，SQL 組裝交給 CrudRepositoryBasic。
    /// - 參數一律走 Dapper 參數化（內建），避免 SQL Injection。
    /// </summary>
    public class TestService : ITestService
    {
        private readonly SQLGenerateHelper _sqlHelper;
        private readonly IPasswordHasher _passwordHasher;
        
        public TestService(SQLGenerateHelper sqlHelper, IPasswordHasher passwordHasher)
        {
            _sqlHelper = sqlHelper; 
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task<List<UserAccount?>> GetTestAsync(CancellationToken ct = default)
        {
            var list = await _sqlHelper.SelectAsync<UserAccount?>(ct);
            return list;
        }

        /// <summary>
        /// Create：新增一個使用者
        /// - 由應用產生 Guid 主鍵（先用 Guid.NewGuid()）
        /// - InsertAsync 會自動把屬性對應到欄位（靠 [Table]/[Column]）
        /// </summary>
        public async Task<Guid> CreateUserAsync(TestController.CreateUserInput input, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(input.Account)) throw new ArgumentException("Account 不可為空");
            if (string.IsNullOrWhiteSpace(input.Name))    throw new ArgumentException("Name 不可為空");

            var salt = _passwordHasher.GenerateSalt();
            var model = UserAccountMapper.ToNewEntity(input, salt, _passwordHasher);
            
            var rows = await _sqlHelper.InsertAsync(model, ct);
            if (rows != 1) throw new InvalidOperationException("Insert 失敗，受影響筆數不是 1");

            return model.Id; // 新建立的 GUID
        }

        /// <summary>
        /// Read：依 Id 讀取一筆
        /// - 用 WhereBuilder.AndEq(x => x.Id, id) 組 WHERE Id = @Id
        /// </summary>
        public async Task<UserAccount?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
        {
            var where = new WhereBuilder<UserAccount>()
                .AndEq(x => x.Id, id);

            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// Read(多筆)：用 Name/Email 模糊查詢
        /// - 傳 null 的條件就不加（這樣比較彈性）
        /// </summary>
        public async Task<List<UserAccount>> SearchUsersAsync(string? name, string? email, CancellationToken ct = default)
        {
            var where = new WhereBuilder<UserAccount>();

            var hasAny = false;
            if (!string.IsNullOrWhiteSpace(name))
            {
                where.AndLike(x => x.Name!, name.Trim());
                hasAny = true;
            }

            // 如果兩個條件都沒傳，避免全表掃描：這裡做一個簡單防呆
            if (!hasAny)
                throw new ArgumentException("請至少提供一個查詢條件（name 或 email）");

            return await _sqlHelper.SelectWhereAsync(where, ct);
        }

        /// <summary>
        /// Update：依主鍵更新整筆（有加 RowVersion 就會做樂觀鎖）
        /// - includeNulls=false（預設）：只更新非 null；避免你把 DB 值誤蓋成 null。
        /// - includeNulls=true：null 也寫回（完整表單提交用）。
        /// - rowVersion：前端要帶舊值（DB 讀出那個），被改過會回 0。
        /// </summary>
        public async Task<int> UpdateUserAsync(
            TestController.UpdateUserInput entity,
            CancellationToken ct = default)
        {
            // 受影響筆數：1=成功；0=找不到 or RowVersion 不合（可能被別人改過）
            return await _sqlHelper.UpdateAllByIdAsync(entity, UpdateNullBehavior.IncludeNulls, ct);
        }

        /// <summary>
        /// Delete：硬刪（真的 DELETE）
        /// - 若你要軟刪，建議自己在 USERS 加 IsDeleted 欄位，然後改成 Update。
        /// </summary>
        public async Task<int> DeleteUserAsync(Guid id, CancellationToken ct = default)
        {
            var where = new WhereBuilder<UserAccount>()
                .AndEq(x => x.Id, id);

            return await _sqlHelper.DeleteWhereAsync(where, ct);
        }
    }
}
