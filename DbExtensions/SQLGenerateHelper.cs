using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Reflection;
using Dapper;
using DcMateH5Api.Services.CurrentUser.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.SqlHelper
{
    /// <summary>
    /// Null 更新策略：
    /// - IncludeNulls：null 也寫回（清空欄位用）
    /// - IgnoreNulls：略過 null（預設）
    /// </summary>
    public enum UpdateNullBehavior { IncludeNulls = 0, IgnoreNulls = 1 }

    /// <summary>
    /// 精簡版 CRUD + Fluent UpdateById。
    /// 僅依賴 DataAnnotations：[Table]、[Column]、[Key]、[Timestamp]。
    ///
    /// 注意：
    /// 1. 本 Helper 會「根據 Entity 的屬性」產生 SQL；SQL 的 Table/Column 名稱來自 Attribute 或 PropertyName。
    /// 2. DeleteWhere 系列採用軟刪除（IS_DELETE = 1）。查詢方法不會自動過濾 IS_DELETE，
    ///    需要自行在 WhereBuilder 加上 AndNotDeleted()。
    /// 3. EnableAuditColumns 若啟用，INSERT/UPDATE 會固定嘗試寫入審計欄位：
    ///    CREATE_USER / CREATE_TIME / EDIT_USER / EDIT_TIME / IS_DELETE。
    ///    → 因為目前不會檢查資料表是否存在這些欄位，
    ///      所以「只有在所有目標表都具備上述欄位」時才建議開啟。
    /// 4. 若 Entity 含 [Timestamp] 欄位，UpdateById 會自動做樂觀鎖，
    ///    呼叫端必須透過 WithRowVersion(...) 傳入原始值，否則會丟 InvalidOperationException。
    /// </summary>
    public sealed class SQLGenerateHelper
    {
        private readonly IDbExecutor _db;
        private readonly ICurrentUserAccessor _currentUser;
        
        public SQLGenerateHelper(IDbExecutor db, ICurrentUserAccessor currentUser)
        { 
            _db = db;
            _currentUser = currentUser; 
        }

        /// <summary>
        /// 是否在 INSERT/UPDATE 自動寫入審計欄位（固定欄位名：CREATE_USER/CREATE_TIME/EDIT_USER/EDIT_TIME/IS_DELETE）。
        /// 目前不會檢查資料表是否存在這些欄位：
        /// - 若資料表缺欄位會直接 SQL error
        /// </summary>
        public bool EnableAuditColumns { get; set; } = true;

        // 審計欄位（資料庫欄位名）
        private const string COL_CREATE_USER = "CREATE_USER";
        private const string COL_CREATE_TIME = "CREATE_TIME";
        private const string COL_EDIT_USER   = "EDIT_USER";
        private const string COL_EDIT_TIME   = "EDIT_TIME";
        private const string COL_IS_DELETE   = "IS_DELETE";

        // 審計欄位（參數名，避免撞 Entity 屬性）
        private const string P_CREATE_USER = "CreateUser";
        private const string P_CREATE_TIME = "CreateTime";
        private const string P_EDIT_USER   = "EditUser";
        private const string P_EDIT_TIME   = "EditTime";
        private const string P_IS_DELETE   = "IsDelete";

        // 目前登入者（JWT sub），抓不到回 Guid.Empty
        private Guid GetCurrentUserId()
        {
            var user = _currentUser.Get();
            return user.Id;
        }

        private Task<DateTime> GetDbNowAsync(CancellationToken ct)
            => _db.ExecuteScalarAsync<DateTime>("SELECT SYSDATETIME();", ct: ct);

        private Task<DateTime> GetDbNowInTxAsync(SqlConnection conn, SqlTransaction tx, CancellationToken ct)
            => _db.ExecuteScalarInTxAsync<DateTime>(conn, tx, "SELECT SYSDATETIME();", ct: ct);

        private static void AddAuditForInsert(
            List<string> cols,
            List<string> vals,
            DynamicParameters dp,
            Guid uid,
            DateTime now)
        {
            AddColIfNotExists(cols, vals, dp, COL_CREATE_USER, P_CREATE_USER, uid);
            AddColIfNotExists(cols, vals, dp, COL_CREATE_TIME, P_CREATE_TIME, now);
            AddColIfNotExists(cols, vals, dp, COL_EDIT_USER,   P_EDIT_USER,   uid);
            AddColIfNotExists(cols, vals, dp, COL_EDIT_TIME,   P_EDIT_TIME,   now);
            AddColIfNotExists(cols, vals, dp, COL_IS_DELETE,   P_IS_DELETE,   false);
        }

        private static void AddAuditForUpdate(List<string> sets, DynamicParameters dp, Guid uid, DateTime now)
        {
            sets.Add($"[{COL_EDIT_USER}] = @{P_EDIT_USER}"); dp.Add(P_EDIT_USER, uid);
            sets.Add($"[{COL_EDIT_TIME}] = @{P_EDIT_TIME}"); dp.Add(P_EDIT_TIME, now);
        }
        
        private static void AddColIfNotExists(
            List<string> cols,
            List<string> vals,
            DynamicParameters dp,
            string colName,
            string paramName,
            object value)
        {
            var colToken = $"[{colName}]";
            if (cols.Any(c => string.Equals(c, colToken, StringComparison.OrdinalIgnoreCase)))
                return;

            cols.Add(colToken);
            vals.Add("@" + paramName);
            dp.Add(paramName, value);
        }

        #region Transaction

        public Task TxAsync(
            Func<SqlConnection, SqlTransaction, CancellationToken, Task> work,
            IsolationLevel isolation = IsolationLevel.ReadCommitted,
            CancellationToken ct = default)
            => _db.TxAsync(work, isolation: isolation, ct: ct);

        public Task<TResult> TxAsync<TResult>(
            Func<SqlConnection, SqlTransaction, CancellationToken, Task<TResult>> work,
            IsolationLevel isolation = IsolationLevel.ReadCommitted,
            CancellationToken ct = default)
            => _db.TxAsync(work, isolation: isolation, ct: ct);

        #endregion
        
        #region Insert

        /// <summary>
        /// 插入一筆資料（短連線）。
        /// - 會排除 [Timestamp] 欄位（rowversion 由 SQL Server 生成）
        /// - 若 EnableAuditColumns=true，會額外寫入審計欄位（需資料表具備對應欄位）
        /// </summary>
        public async Task<int> InsertAsync<T>(T entity, CancellationToken ct = default)
        {
            var (table, props, rowVersion, _, colByProp) = Reflect<T>();

            var toInsert = props.Where(p => p != rowVersion).ToList();
            var cols = new List<string>(toInsert.Select(p => $"[{colByProp[p.Name]}]"));
            var vals = new List<string>(toInsert.Select(p => "@" + p.Name));
            var dp = new DynamicParameters(entity);

            if (EnableAuditColumns)
            {
                var id = GetCurrentUserId();
                var now = await GetDbNowAsync(ct);
                AddAuditForInsert(cols, vals, dp, id, now);
            }

            var sql = $"INSERT INTO {table} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)});";
            return await _db.ExecuteAsync(sql, dp, ct: ct);
        }

        public async Task<int> InsertInTxAsync<T>(
            SqlConnection conn,
            SqlTransaction tx,
            T entity,
            int? timeoutSeconds = null,
            CancellationToken ct = default)
        {
            var (table, props, rowVersion, _, colByProp) = Reflect<T>();

            var toInsert = props.Where(p => p != rowVersion).ToList();
            var cols = new List<string>(toInsert.Select(p => $"[{colByProp[p.Name]}]"));
            var vals = new List<string>(toInsert.Select(p => "@" + p.Name));
            var dp = new DynamicParameters(entity);

            if (EnableAuditColumns)
            {
                var uid = GetCurrentUserId();
                var now = await GetDbNowInTxAsync(conn, tx, ct);
                AddAuditForInsert(cols, vals, dp, uid, now);
            }

            var sql = $"INSERT INTO {table} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)});";
            return await _db.ExecuteInTxAsync(conn, tx, sql, dp, timeoutSeconds: timeoutSeconds, commandType: CommandType.Text, ct: ct);
        }

        public async Task<long?> InsertAndGetIdentityAsync<T>(T entity, bool enableAuditColumns = false, CancellationToken ct = default)
        {
            var (table, props, rowVersion, _, colByProp) = Reflect<T>();

            var toInsert = props.Where(p => p != rowVersion).ToList();
            var cols = new List<string>(toInsert.Select(p => $"[{colByProp[p.Name]}]"));
            var vals = new List<string>(toInsert.Select(p => "@" + p.Name));
            var dp = new DynamicParameters(entity);

            if (enableAuditColumns)
            {
                var uid = GetCurrentUserId();
                var now = await GetDbNowAsync(ct);
                AddAuditForInsert(cols, vals, dp, uid, now);
            }

            var sql = $"INSERT INTO {table} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)}); SELECT CAST(SCOPE_IDENTITY() AS bigint);";
            return await _db.ExecuteScalarAsync<long?>(sql, dp, ct: ct);
        }

        public async Task<long?> InsertAndGetIdentityInTxAsync<T>(
            SqlConnection conn,
            SqlTransaction tx,
            T entity,
            bool enableAuditColumns = false,
            int? timeoutSeconds = null,
            CancellationToken ct = default)
        {
            var (table, props, rowVersion, _, colByProp) = Reflect<T>();

            var toInsert = props.Where(p => p != rowVersion).ToList();
            var cols = new List<string>(toInsert.Select(p => $"[{colByProp[p.Name]}]"));
            var vals = new List<string>(toInsert.Select(p => "@" + p.Name));
            var dp = new DynamicParameters(entity);

            if (enableAuditColumns)
            {
                var uid = GetCurrentUserId();
                var now = await GetDbNowInTxAsync(conn, tx, ct);
                AddAuditForInsert(cols, vals, dp, uid, now);
            }

            var sql = $"INSERT INTO {table} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)}); SELECT CAST(SCOPE_IDENTITY() AS bigint);";
            return await _db.ExecuteScalarInTxAsync<long?>(conn, tx, sql, dp, timeoutSeconds: timeoutSeconds, commandType: CommandType.Text, ct: ct);
        }

        #endregion
        
        #region Update

        /// <summary>
        /// Fluent 入口：以主鍵 Id 啟動更新。
        /// 例：UpdateById&lt;Foo&gt;(id).Set(x =&gt; x.Name, "A").Audit().ExecuteAsync();
        /// </summary>
        public UpdateByIdBuilder<T> UpdateById<T>(object id) => new(this, id);

        /// <summary>
        /// 舊版整筆更新（以 Entity 當作更新來源）。
        /// 建議只用於「明確要整筆覆蓋」的情境；一般局部更新請改用 UpdateById。
        ///
        /// - mode = IncludeNulls：entity 的 null 會寫回 DB（可用來清空欄位）
        /// - mode = IgnoreNulls：entity 的 null 會被略過（避免誤清空）
        /// - 若 Entity 含 [Timestamp]，會把 rowversion 放進 WHERE 做樂觀鎖
        /// </summary>
        [Obsolete("如果要針對特定欄位進行更新，使用UpdateById")]
        public async Task<int> UpdateAllByIdAsync<T>(
            T entity,
            UpdateNullBehavior mode = UpdateNullBehavior.IncludeNulls,
            bool enableAuditColumns = false,
            CancellationToken ct = default)
        {
            var (table, props, rowVersion, key, colByProp) = Reflect<T>();

            var updatable = props.Where(p => p != key && p != rowVersion).ToList();
            if (mode == UpdateNullBehavior.IgnoreNulls)
                updatable = updatable.Where(p => p.GetValue(entity) != null).ToList();

            if (updatable.Count == 0)
                throw new ArgumentException("沒有可更新的欄位（可能全部為 null 且 IgnoreNulls）");

            var sets = new List<string>(updatable.Select(p => $"[{colByProp[p.Name]}] = @{p.Name}"));
            var dp = new DynamicParameters(entity);

            if (enableAuditColumns)
            {
                var uid = GetCurrentUserId();
                var now = await GetDbNowAsync(ct);
                AddAuditForUpdate(sets, dp, uid, now);
            }

            var where = $"[{colByProp[key.Name]}] = @{key.Name}";
            if (rowVersion != null)
                where += $" AND [{colByProp[rowVersion.Name]}] = @{rowVersion.Name}";

            var sql = $"UPDATE {table} SET {string.Join(", ", sets)} WHERE {where};";
            return await _db.ExecuteAsync(sql, dp, ct: ct);
        }

        public async Task<int> UpdateAllByIdInTxAsync<T>(
            SqlConnection conn,
            SqlTransaction tx,
            T entity,
            UpdateNullBehavior mode = UpdateNullBehavior.IncludeNulls,
            bool enableAuditColumns = false,
            int? timeoutSeconds = null,
            CancellationToken ct = default)
        {
            var (table, props, rowVersion, key, colByProp) = Reflect<T>();

            var updatable = props.Where(p => p != key && p != rowVersion).ToList();
            if (mode == UpdateNullBehavior.IgnoreNulls)
                updatable = updatable.Where(p => p.GetValue(entity) != null).ToList();

            if (updatable.Count == 0)
                throw new ArgumentException("沒有可更新的欄位（可能全部為 null 且 IgnoreNulls）");

            var sets = new List<string>(updatable.Select(p => $"[{colByProp[p.Name]}] = @{p.Name}"));
            var dp = new DynamicParameters(entity);

            if (enableAuditColumns)
            {
                var uid = GetCurrentUserId();
                var now = await GetDbNowInTxAsync(conn, tx, ct);
                AddAuditForUpdate(sets, dp, uid, now);
            }

            var where = $"[{colByProp[key.Name]}] = @{key.Name}";
            if (rowVersion != null)
                where += $" AND [{colByProp[rowVersion.Name]}] = @{rowVersion.Name}";

            var sql = $"UPDATE {table} SET {string.Join(", ", sets)} WHERE {where};";
            return await _db.ExecuteInTxAsync(conn, tx, sql, dp, timeoutSeconds: timeoutSeconds, commandType: CommandType.Text, ct: ct);
        }

        #endregion
        
        #region Select / Delete

        /// <summary>
        /// 查詢全表（不會自動排除軟刪除資料）。
        /// 若使用 DeleteWhere 進行軟刪除，查詢建議搭配 WhereBuilder.AndNotDeleted()。
        /// </summary>
        public async Task<List<T>> SelectAsync<T>(CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            return await _db.QueryAsync<T>($"SELECT {cols} FROM {table};", ct: ct);
        }

        public async Task<List<T>> SelectInTxAsync<T>(
            SqlConnection conn,
            SqlTransaction tx,
            int? timeoutSeconds = null,
            CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            return await _db.QueryInTxAsync<T>(conn, tx, $"SELECT {cols} FROM {table};",
                param: null, timeoutSeconds: timeoutSeconds, commandType: CommandType.Text, ct: ct);
        }

        public async Task<List<T>> SelectWhereAsync<T>(WhereBuilder<T> where, CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            var (w, param) = where.Build();
            return await _db.QueryAsync<T>($"SELECT {cols} FROM {table} {w};", param, ct: ct);
        }

        public async Task<List<T>> SelectWhereInTxAsync<T>(
            SqlConnection conn,
            SqlTransaction tx,
            WhereBuilder<T> where,
            int? timeoutSeconds = null,
            CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            var (w, param) = where.Build();
            var sql = $"SELECT {cols} FROM {table} {w};";
            return await _db.QueryInTxAsync<T>(conn, tx, sql, param, timeoutSeconds: timeoutSeconds, commandType: CommandType.Text, ct: ct);
        }

        public async Task<T?> SelectFirstOrDefaultAsync<T>(WhereBuilder<T> where, CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            var (w, param) = where.Build();
            return await _db.QueryFirstOrDefaultAsync<T>($"SELECT TOP (1) {cols} FROM {table} {w};", param, ct: ct);
        }

        public async Task<T?> SelectFirstOrDefaultInTxAsync<T>(
            SqlConnection conn,
            SqlTransaction tx,
            WhereBuilder<T> where,
            int? timeoutSeconds = null,
            CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            var (w, param) = where.Build();
            var sql = $"SELECT TOP (1) {cols} FROM {table} {w};";
            return await _db.QueryFirstOrDefaultInTxAsync<T>(conn, tx, sql, param, timeoutSeconds: timeoutSeconds, commandType: CommandType.Text, ct: ct);
        }

        /// <summary>
        /// 判斷是否存在符合條件的資料（短連線）。
        /// </summary>
        public async Task<bool> ExistsAsync<T>(WhereBuilder<T> where, CancellationToken ct = default)
        {
            var (table, _, _, _, _) = Reflect<T>();
            var (w, param) = where.Build();
            var sql = $"SELECT TOP (1) 1 FROM {table} {w};";
            var res = await _db.ExecuteScalarAsync<int?>(sql, param, ct: ct);
            return res.HasValue;
        }

        /// <summary>
        /// 判斷是否存在符合條件的資料（交易內版本）。
        /// </summary>
        public async Task<bool> ExistsInTxAsync<T>(
            SqlConnection conn,
            SqlTransaction tx,
            WhereBuilder<T> where,
            int? timeoutSeconds = null,
            CancellationToken ct = default)
        {
            var (table, _, _, _, _) = Reflect<T>();
            var (w, param) = where.Build();
            var sql = $"SELECT TOP (1) 1 FROM {table} {w};";
            var res = await _db.ExecuteScalarInTxAsync<int?>(conn, tx, sql, param, timeoutSeconds: timeoutSeconds, commandType: CommandType.Text, ct: ct);
            return res.HasValue;
        }

        /// <summary>
        /// 軟刪除（IS_DELETE = 1）。
        /// 本方法不會自動加上 IS_DELETE = 0 條件，
        ///    若需要「只刪除未刪除資料」請自行在 where 加上 AndNotDeleted()。
        /// </summary>
        public async Task<int> DeleteWhereAsync<T>(WhereBuilder<T> where, CancellationToken ct = default)
        {
            var (table, _, _, _, _) = Reflect<T>();
            var (w, param) = where.Build();
            return await _db.ExecuteAsync($"UPDATE {table} SET [{COL_IS_DELETE}] = 1 {w};", param, ct: ct);
        }

        public async Task<int> DeleteWhereInTxAsync<T>(
            SqlConnection conn,
            SqlTransaction tx,
            WhereBuilder<T> where,
            int? timeoutSeconds = null,
            CancellationToken ct = default)
        {
            var (table, _, _, _, _) = Reflect<T>();
            var (w, param) = where.Build();
            var sql = $"UPDATE {table} SET [{COL_IS_DELETE}] = 1 {w};";
            return await _db.ExecuteInTxAsync(conn, tx, sql, param, timeoutSeconds: timeoutSeconds, commandType: CommandType.Text, ct: ct);
        }

        /// <summary>
        /// [ !!!!謹慎使用 ] 物理刪除（Tx 版本）
        /// </summary>
        public async Task<int> DeletePhysicalWhereInTxAsync<T>(
            SqlConnection conn,
            SqlTransaction tx,
            WhereBuilder<T> where,
            int? timeoutSeconds = null,
            CancellationToken ct = default)
        {
            var (table, _, _, _, _) = Reflect<T>();
            var (w, param) = where.Build();

            if (string.IsNullOrWhiteSpace(w))
                throw new InvalidOperationException(
                    $"DeletePhysicalWhereInTxAsync<{typeof(T).Name}> 必須提供 WHERE 條件，禁止全表物理刪除。");

            var sql = $"DELETE FROM {table} {w};";
            return await _db.ExecuteInTxAsync(conn, tx, sql, param, timeoutSeconds: timeoutSeconds, commandType: CommandType.Text, ct: ct);
        }

        #endregion 
        
        #region 反射快取 

        private static readonly ConcurrentDictionary<Type, (string table, List<PropertyInfo> props, PropertyInfo? rowVersion, PropertyInfo key, Dictionary<string, string> colByProp)>
            _metaCache = new();

        internal static (string table, List<PropertyInfo> props, PropertyInfo? rowVersion, PropertyInfo key, Dictionary<string, string> colByProp) Reflect<T>()
        {
            return _metaCache.GetOrAdd(typeof(T), _ =>
            {
                var t = typeof(T);

                var tableAttr = t.GetCustomAttribute<TableAttribute>();
                var rawName = tableAttr?.Name ?? t.Name;
                var schema = tableAttr?.Schema;
                string Quote(string s) => $"[{s}]";
                var tableFull = schema is null ? Quote(rawName) : $"{Quote(schema)}.{Quote(rawName)}";

                // 只取 public instance，且排除 [NotMapped]
                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null)
                             .ToList();

                var key = props.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null)
                          ?? throw new InvalidOperationException($"{t.Name} 缺少 [Key] 屬性。");

                var rowVersion = props.FirstOrDefault(p => p.GetCustomAttribute<TimestampAttribute>() != null);

                var colByProp = props.ToDictionary(
                    p => p.Name,
                    p => p.GetCustomAttribute<ColumnAttribute>()?.Name ?? p.Name,
                    StringComparer.OrdinalIgnoreCase);

                return (tableFull, props, rowVersion, key, colByProp);
            });
        }

        #endregion
        
        // -------------------- Fluent Update Builder --------------------

        public sealed class UpdateByIdBuilder<T>
        {
            private readonly SQLGenerateHelper _h;
            private readonly object _id;

            private readonly List<(PropertyInfo Prop, object? Value)> _setters = new();
            private UpdateNullBehavior _mode = UpdateNullBehavior.IgnoreNulls;
            private bool _audit;
            private byte[]? _rowVersion; // 若有 [Timestamp]，需帶原始值

            internal UpdateByIdBuilder(SQLGenerateHelper h, object id) { _h = h; _id = id; }

            /// <summary>指定要更新的欄位與值（型別安全）</summary>
            public UpdateByIdBuilder<T> Set<TVal>(Expression<Func<T, object>> field, TVal value)
            {
                _setters.Add((GetProperty(field), value));
                return this;
            }

            /// <summary>略過 null（預設）</summary>
            public UpdateByIdBuilder<T> IgnoreNulls() { _mode = UpdateNullBehavior.IgnoreNulls; return this; }

            /// <summary>包含 null（把欄位清為 NULL 時使用）</summary>
            public UpdateByIdBuilder<T> IncludeNulls() { _mode = UpdateNullBehavior.IncludeNulls; return this; }

            /// <summary>寫入 EDIT_USER/EDIT_TIME</summary>
            public UpdateByIdBuilder<T> Audit(bool enable = true) { _audit = enable; return this; }

            /// <summary>若 T 有 [Timestamp] 欄位，請帶前端取得的原始值</summary>
            public UpdateByIdBuilder<T> WithRowVersion(byte[]? rv) { _rowVersion = rv; return this; }

            /// <summary>組 SQL 並執行（短連線版本）</summary>
            public async Task<int> ExecuteAsync(CancellationToken ct = default)
            {
                var (table, _, rowVersion, key, colByProp) = Reflect<T>();

                if (_setters.Count == 0)
                    throw new InvalidOperationException("請至少 Set 一個欄位。");

                var candidates = _setters
                    .Where(s => s.Prop != key && (rowVersion == null || s.Prop != rowVersion))
                    .GroupBy(s => s.Prop.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .ToList();

                if (_mode == UpdateNullBehavior.IgnoreNulls)
                    candidates = candidates.Where(s => s.Value != null).ToList();

                if (candidates.Count == 0)
                    throw new ArgumentException("沒有可更新的欄位（可能皆為 null 且 IgnoreNulls）。");

                var sets = new List<string>(capacity: candidates.Count + 2);
                var dp = new DynamicParameters();

                foreach (var (prop, val) in candidates)
                {
                    sets.Add($"[{colByProp[prop.Name]}] = @{prop.Name}");
                    dp.Add(prop.Name, val);
                }

                if (_audit)
                {
                    var uid = _h.GetCurrentUserId();
                    var now = await _h.GetDbNowAsync(ct);
                    AddAuditForUpdate(sets, dp, uid, now);
                }

                dp.Add(key.Name, ConvertTo(key.PropertyType, _id));
                var where = $"[{colByProp[key.Name]}] = @{key.Name}";

                if (rowVersion != null)
                {
                    if (_rowVersion is null) throw new InvalidOperationException("此實體含 [Timestamp]，請呼叫 WithRowVersion(...) 帶原值。");
                    dp.Add(rowVersion.Name, _rowVersion);
                    where += $" AND [{colByProp[rowVersion.Name]}] = @{rowVersion.Name}";
                }

                var sql = $"UPDATE {table} SET {string.Join(", ", sets)} WHERE {where};";
                return await _h._db.ExecuteAsync(sql, dp, ct: ct);
            }

            /// <summary>組 SQL 並執行（Tx 版本）</summary>
            public async Task<int> ExecuteInTxAsync(
                SqlConnection conn,
                SqlTransaction tx,
                int? timeoutSeconds = null,
                CancellationToken ct = default)
            {
                var (table, _, rowVersion, key, colByProp) = Reflect<T>();

                if (_setters.Count == 0)
                    throw new InvalidOperationException("請至少 Set 一個欄位。");

                var candidates = _setters
                    .Where(s => s.Prop != key && (rowVersion == null || s.Prop != rowVersion))
                    .GroupBy(s => s.Prop.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .ToList();

                if (_mode == UpdateNullBehavior.IgnoreNulls)
                    candidates = candidates.Where(s => s.Value != null).ToList();

                if (candidates.Count == 0)
                    throw new ArgumentException("沒有可更新的欄位（可能皆為 null 且 IgnoreNulls）。");

                var sets = new List<string>(capacity: candidates.Count + 2);
                var dp = new DynamicParameters();

                foreach (var (prop, val) in candidates)
                {
                    sets.Add($"[{colByProp[prop.Name]}] = @{prop.Name}");
                    dp.Add(prop.Name, val);
                }

                if (_audit)
                {
                    var uid = _h.GetCurrentUserId();
                    var now = await _h.GetDbNowInTxAsync(conn, tx, ct);
                    AddAuditForUpdate(sets, dp, uid, now);
                }

                dp.Add(key.Name, ConvertTo(key.PropertyType, _id));
                var where = $"[{colByProp[key.Name]}] = @{key.Name}";

                if (rowVersion != null)
                {
                    if (_rowVersion is null) throw new InvalidOperationException("此實體含 [Timestamp]，請呼叫 WithRowVersion(...) 帶原值。");
                    dp.Add(rowVersion.Name, _rowVersion);
                    where += $" AND [{colByProp[rowVersion.Name]}] = @{rowVersion.Name}";
                }

                var sql = $"UPDATE {table} SET {string.Join(", ", sets)} WHERE {where};";
                return await _h._db.ExecuteInTxAsync(conn, tx, sql, dp, timeoutSeconds: timeoutSeconds, commandType: CommandType.Text, ct: ct);
            }

            internal static PropertyInfo GetProperty(Expression<Func<T, object>> selector)
            {
                if (selector.Body is MemberExpression me && me.Member is PropertyInfo pi) return pi;
                if (selector.Body is UnaryExpression ue && ue.Operand is MemberExpression me2 && me2.Member is PropertyInfo pi2) return pi2;
                throw new ArgumentException("只支援 x => x.Prop 的寫法");
            }

            private static object? ConvertTo(Type t, object? value)
            {
                if (value is null) return null;

                var u = Nullable.GetUnderlyingType(t) ?? t;

                if (u == typeof(Guid))
                {
                    if (value is Guid g) return g;
                    if (value is string s && Guid.TryParse(s, out var gid)) return gid;
                    throw new InvalidCastException($"無法將 {value} 轉為 Guid");
                }

                if (u.IsEnum)
                {
                    if (value is string es) return Enum.Parse(u, es, ignoreCase: true);
                    return Enum.ToObject(u, value);
                }

                return u == value.GetType() ? value : System.Convert.ChangeType(value, u);
            }

        }
    }

    /// <summary>
    /// AND-only Where Builder（欄位用 Lambda；值一律參數化，防 SQL Injection）
    ///
    /// 限制與設計：
    /// 1. 只支援 AND 串接（不支援 OR / 括號群組）
    /// 2. Build() 會強制至少 1 個條件，避免誤刪 / 誤更整張表
    /// 3. 常用軟刪除條件可用 AndNotDeleted()（IS_DELETE = 0）
    /// </summary>
    public sealed class WhereBuilder<T>
    {
        private readonly List<string> _clauses = new();
        private readonly DynamicParameters _param = new();
        private int _idx = 0;

        public WhereBuilder<T> AndEq(Expression<Func<T, object>> field, object? value)
        {
            var col = GetColumnName(field);
            if (value is null) _clauses.Add($"[{col}] IS NULL");
            else
            {
                var p = Next(col);
                _clauses.Add($"[{col}] = @{p}");
                _param.Add(p, value);
            }
            return this;
        }

        public WhereBuilder<T> AndLike(Expression<Func<T, object>> field, string keyword, bool surround = true)
        {
            var col = GetColumnName(field);
            var p = Next(col);
            _clauses.Add($"[{col}] LIKE @{p}");
            _param.Add(p, surround ? $"%{keyword}%" : keyword);
            return this;
        }

        public WhereBuilder<T> AndIn<TVal>(Expression<Func<T, object>> field, IEnumerable<TVal> values)
        {
            var list = values?.ToList() ?? throw new ArgumentNullException(nameof(values));
            if (list.Count == 0)
            {
                _clauses.Add("1 = 0"); // 避免 IN ()，且語意是「不可能命中」
                return this;
            }

            var col = GetColumnName(field);
            var p = Next(col);
            _clauses.Add($"[{col}] IN @{p}");
            _param.Add(p, list);
            return this;
        }

        public WhereBuilder<T> AndGt(Expression<Func<T, object>> f, object v) => Add(f, ">", v);
        public WhereBuilder<T> AndGte(Expression<Func<T, object>> f, object v) => Add(f, ">=", v);
        public WhereBuilder<T> AndLt(Expression<Func<T, object>> f, object v) => Add(f, "<", v);
        public WhereBuilder<T> AndLte(Expression<Func<T, object>> f, object v) => Add(f, "<=", v);

        public WhereBuilder<T> AndBetween(Expression<Func<T, object>> field, object from, object to)
        {
            var col = GetColumnName(field);
            var p1 = Next(col + "_from"); var p2 = Next(col + "_to");
            _clauses.Add($"[{col}] BETWEEN @{p1} AND @{p2}");
            _param.Add(p1, from); _param.Add(p2, to);
            return this;
        }

        /// <summary>軟刪除常用：IS_DELETE = 0</summary>
        public WhereBuilder<T> AndNotDeleted()
        {
            const string col = "IS_DELETE";
            var p = Next(col);
            _clauses.Add($"[{col}] = @{p}");
            _param.Add(p, 0);
            return this;
        }

        internal (string sql, DynamicParameters param) Build()
        {
            if (_clauses.Count == 0) throw new InvalidOperationException("WHERE 需要至少一個條件（避免誤刪/誤更整張表）");
            return ("WHERE " + string.Join(" AND ", _clauses), _param);
        }

        private WhereBuilder<T> Add(Expression<Func<T, object>> field, string op, object value)
        {
            var col = GetColumnName(field);
            var p = Next(col);
            _clauses.Add($"[{col}] {op} @{p}");
            _param.Add(p, value);
            return this;
        }

        private static string GetPropertyName(Expression<Func<T, object>> selector) =>
            selector.Body switch
            {
                MemberExpression me => me.Member.Name,
                UnaryExpression { Operand: MemberExpression me2 } => me2.Member.Name,
                _ => throw new ArgumentException("只支援 x => x.Prop 的寫法")
            };

        private static string GetColumnName(Expression<Func<T, object>> selector)
        {
            var propName = GetPropertyName(selector);
            var (_, props, _, _, colByProp) = SQLGenerateHelper.Reflect<T>();
            var prop = props.FirstOrDefault(p => p.Name == propName)
                       ?? throw new ArgumentException($"找不到屬性：{propName}");
            return colByProp[prop.Name];
        }

        private string Next(string baseName) => $"{baseName}_{_idx++}";
    }
}
