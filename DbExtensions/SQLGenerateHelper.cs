using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Dapper;
using Microsoft.AspNetCore.Http;

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
    /// </summary>
    public sealed class SQLGenerateHelper
    {
        private readonly IDbExecutor _db;
        private readonly IHttpContextAccessor _http;

        public SQLGenerateHelper(IDbExecutor db, IHttpContextAccessor http)
        { _db = db; _http = http; }

        // 是否在 INSERT 自動寫入 5 個審計欄位（若表上有）
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
        private Guid CurrentUserIdOrEmpty()
        {
            var raw = _http.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(raw, out var gid) ? gid : Guid.Empty;
        }

        private Task<DateTime> GetDbUtcNowAsync(CancellationToken ct)
            => _db.ExecuteScalarAsync<DateTime>("SELECT SYSUTCDATETIME();", ct: ct);

        private static void AddAuditForInsert(List<string> cols, List<string> vals, DynamicParameters dp, Guid uid, DateTime now)
        {
            cols.Add($"[{COL_CREATE_USER}]"); vals.Add($"@{P_CREATE_USER}"); dp.Add(P_CREATE_USER, uid);
            cols.Add($"[{COL_CREATE_TIME}]"); vals.Add($"@{P_CREATE_TIME}"); dp.Add(P_CREATE_TIME, now);
            cols.Add($"[{COL_EDIT_USER}]");   vals.Add($"@{P_EDIT_USER}");   dp.Add(P_EDIT_USER, uid);
            cols.Add($"[{COL_EDIT_TIME}]");   vals.Add($"@{P_EDIT_TIME}");   dp.Add(P_EDIT_TIME, now);
            cols.Add($"[{COL_IS_DELETE}]");   vals.Add($"@{P_IS_DELETE}");   dp.Add(P_IS_DELETE, false);
        }

        private static void AddAuditForUpdate(List<string> sets, DynamicParameters dp, Guid uid, DateTime now)
        {
            sets.Add($"[{COL_EDIT_USER}] = @{P_EDIT_USER}"); dp.Add(P_EDIT_USER, uid);
            sets.Add($"[{COL_EDIT_TIME}] = @{P_EDIT_TIME}"); dp.Add(P_EDIT_TIME, now);
        }

        // --------------------------- Insert ---------------------------

        public async Task<int> InsertAsync<T>(T entity, CancellationToken ct = default)
        {
            var (table, props, rowVersion, _, colByProp) = Reflect<T>();

            var toInsert = props.Where(p => p != rowVersion).ToList();
            var cols = new List<string>(toInsert.Select(p => $"[{colByProp[p.Name]}]"));
            var vals = new List<string>(toInsert.Select(p => "@" + p.Name));
            var dp = new DynamicParameters(entity);

            if (EnableAuditColumns)
            {
                var id = CurrentUserIdOrEmpty();
                var now = await GetDbUtcNowAsync(ct);
                AddAuditForInsert(cols, vals, dp, id, now);
            }

            var sql = $"INSERT INTO {table} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)});";
            return await _db.ExecuteAsync(sql, dp, ct: ct);
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
                var uid = CurrentUserIdOrEmpty();
                var now = await GetDbUtcNowAsync(ct);
                AddAuditForInsert(cols, vals, dp, uid, now);
            }

            var sql = $"INSERT INTO {table} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)}); SELECT CAST(SCOPE_IDENTITY() AS bigint);";
            return await _db.ExecuteScalarAsync<long?>(sql, dp, ct: ct);
        }

        // --------------------------- Update ---------------------------

        /// <summary>
        /// Fluent 入口：以主鍵 Id 啟動更新。
        /// 例：UpdateById&lt;Foo&gt;(id).Set(x =&gt; x.Name, "A").Audit().ExecuteAsync();
        /// </summary>
        public UpdateByIdBuilder<T> UpdateById<T>(object id) => new(this, id);

        /// <summary>
        /// 舊版整筆更新（保留相容；建議改用 UpdateById）。 
        /// </summary>
        [Obsolete("如果要針對特定欄位進行更新，使用UpdateById")]
        public async Task<int> UpdateAllByIdAsync<T>(T entity, UpdateNullBehavior mode = UpdateNullBehavior.IncludeNulls, bool enableAuditColumns = false, CancellationToken ct = default)
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
                var uid = CurrentUserIdOrEmpty();
                var now = await GetDbUtcNowAsync(ct);
                AddAuditForUpdate(sets, dp, uid, now);
            }

            var where = $"[{colByProp[key.Name]}] = @{key.Name}";
            if (rowVersion != null)
                where += $" AND [{colByProp[rowVersion.Name]}] = @{rowVersion.Name}";

            var sql = $"UPDATE {table} SET {string.Join(", ", sets)} WHERE {where};";
            return await _db.ExecuteAsync(sql, dp, ct: ct);
        }

        // ---------------------- Select / Delete -----------------------

        public async Task<List<T>> SelectAsync<T>(CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            return await _db.QueryAsync<T>($"SELECT {cols} FROM {table};", ct: ct);
        }

        public async Task<List<T>> SelectWhereAsync<T>(WhereBuilder<T> where, CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            var (w, param) = where.Build();
            return await _db.QueryAsync<T>($"SELECT {cols} FROM {table} {w};", param, ct: ct);
        }

        public async Task<T?> SelectFirstOrDefaultAsync<T>(WhereBuilder<T> where, CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            var (w, param) = where.Build();
            return await _db.QueryFirstOrDefaultAsync<T>($"SELECT TOP (1) {cols} FROM {table} {w};", param, ct: ct);
        }

        public async Task<int> DeleteWhereAsync<T>(WhereBuilder<T> where, CancellationToken ct = default)
        {
            var (table, _, _, _, _) = Reflect<T>();
            var (w, param) = where.Build();
            return await _db.ExecuteAsync($"UPDATE {table} SET [{COL_IS_DELETE}] = 1 {w};", param, ct: ct);
        }

        // ------------------------ 反射快取 ------------------------

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

            /// <summary>組 SQL 並執行</summary>
            public async Task<int> ExecuteAsync(CancellationToken ct = default)
            {
                var (table, props, rowVersion, key, colByProp) = Reflect<T>();

                if (_setters.Count == 0)
                    throw new InvalidOperationException("請至少 Set 一個欄位。");

                // 移除不可更新欄位（主鍵/RowVersion），同欄位以最後一次 Set 為準
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
                var dp   = new DynamicParameters();

                foreach (var (prop, val) in candidates)
                {
                    sets.Add($"[{colByProp[prop.Name]}] = @{prop.Name}");
                    dp.Add(prop.Name, val);
                }

                if (_audit)
                {
                    var uid = _h.CurrentUserIdOrEmpty();
                    var now = await _h.GetDbUtcNowAsync(ct);
                    AddAuditForUpdate(sets, dp, uid, now);
                }

                // WHERE 主鍵
                dp.Add(key.Name, ConvertTo(key.PropertyType, _id));
                var where = $"[{colByProp[key.Name]}] = @{key.Name}";

                // 若有 RowVersion，強制要求呼叫端帶原值做樂觀鎖
                if (rowVersion != null)
                {
                    if (_rowVersion is null) throw new InvalidOperationException("此實體含 [Timestamp]，請呼叫 WithRowVersion(...) 帶原值。");
                    dp.Add(rowVersion.Name, _rowVersion);
                    where += $" AND [{colByProp[rowVersion.Name]}] = @{rowVersion.Name}";
                }

                var sql = $"UPDATE {table} SET {string.Join(", ", sets)} WHERE {where};";
                return await _h._db.ExecuteAsync(sql, dp, ct: ct);
            }

            // 取出屬性資訊（支援 x => x.Prop / 裝箱）
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
                if (u.IsEnum) return Enum.ToObject(u, value);
                return u == value.GetType() ? value : System.Convert.ChangeType(value, u);
            }
        }
    }

    /// <summary>
    /// AND-only Where Builder（欄位用 Lambda；值一律參數化，防 SQL Injection）
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
            var col = GetColumnName(field);
            var p = Next(col);
            _clauses.Add($"[{col}] IN @{p}"); // Dapper 會展開 IEnumerable
            _param.Add(p, values);
            return this;
        }

        public WhereBuilder<T> AndGt (Expression<Func<T, object>> f, object v) => Add(f, ">",  v);
        public WhereBuilder<T> AndGte(Expression<Func<T, object>> f, object v) => Add(f, ">=", v);
        public WhereBuilder<T> AndLt (Expression<Func<T, object>> f, object v) => Add(f, "<",  v);
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
