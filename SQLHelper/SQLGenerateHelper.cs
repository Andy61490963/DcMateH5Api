using System.ComponentModel.DataAnnotations;        
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using Dapper;

namespace DcMateH5Api.SqlHelper
{
    /// <summary>
    /// 更新 Null 的策略：
    /// - IncludeNulls：把 null 也寫回資料庫（整張表單送出時常用）
    /// - IgnoreNulls：略過為 null 的屬性（避免不小心把 DB 值蓋掉）
    /// </summary>
    public enum UpdateNullBehavior
    {
        IncludeNulls = 0,
        IgnoreNulls = 1
    }

    /// <summary>
    /// 最簡版的 CRUD：Insert / UpdateAllById / SelectWhere / DeleteWhere
    /// 只用 4 個屬性：[Table]、[Column]、[Key]、[Timestamp]
    /// </summary>
    public sealed class SQLGenerateHelper
    {
        private readonly IDbExecutor _db;
        private readonly IHttpContextAccessor _http; 
        
        public SQLGenerateHelper(IDbExecutor db, IHttpContextAccessor http)
        {
            _db = db;
            _http = http;
        }

        // === 1) 旗標：控制是否寫入 5 個審計欄位 ===
        public bool EnableAuditColumns { get; set; } = true; // 需要加就 true；表上沒有就設 false

        // === 2) 欄位常數（DB 欄位名） ===
        private const string COL_CREATE_USER = "CREATE_USER";
        private const string COL_CREATE_TIME = "CREATE_TIME";
        private const string COL_EDIT_USER   = "EDIT_USER";
        private const string COL_EDIT_TIME   = "EDIT_TIME";
        private const string COL_IS_DELETE   = "IS_DELETE";

        // 參數名（避免和實體屬性撞名）
        private const string P_CREATE_USER = "CreateUser";
        private const string P_CREATE_TIME = "CreateTime";
        private const string P_EDIT_USER   = "EditUser";
        private const string P_EDIT_TIME   = "EditTime";
        private const string P_IS_DELETE   = "IsDelete";
        
        /// <summary>
        /// 取得目前使用者 Id（JWT sub / NameIdentifier），抓不到就 Guid.Empty
        /// </summary>
        /// <returns></returns>
        private Guid CurrentUserIdOrEmpty()
        {
            var u = _http.HttpContext?.User;
            var raw = u?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(raw, out var gid) ? gid : Guid.Empty;
        }

        private Task<DateTime> GetDbUtcNowAsync(CancellationToken ct) =>
            _db.ExecuteScalarAsync<DateTime>("SELECT SYSUTCDATETIME();", ct: ct);
        
        /// <summary>
        /// 在 INSERT SQL 組裝階段加預設欄位
        /// </summary>
        /// <param name="cols"></param>
        /// <param name="vals"></param>
        /// <param name="param"></param>
        /// <param name="uid"></param>
        /// <param name="now"></param>
        private void AppendAuditForInsert(List<string> cols, List<string> vals, DynamicParameters param, Guid uid, DateTime now)
        {
            cols.Add($"[{COL_CREATE_USER}]"); vals.Add($"@{P_CREATE_USER}"); param.Add(P_CREATE_USER, uid);
            cols.Add($"[{COL_CREATE_TIME}]"); vals.Add($"@{P_CREATE_TIME}"); param.Add(P_CREATE_TIME, now);
            cols.Add($"[{COL_EDIT_USER}]");   vals.Add($"@{P_EDIT_USER}");   param.Add(P_EDIT_USER, uid);
            cols.Add($"[{COL_EDIT_TIME}]");   vals.Add($"@{P_EDIT_TIME}");   param.Add(P_EDIT_TIME, now);
            cols.Add($"[{COL_IS_DELETE}]");   vals.Add($"@{P_IS_DELETE}");   param.Add(P_IS_DELETE, false);
        }

        /// <summary>
        /// 在 UPDATE SQL 組裝階段加預設欄位
        /// </summary>
        /// <param name="sets"></param>
        /// <param name="param"></param>
        /// <param name="uid"></param>
        /// <param name="now"></param>
        private void AppendAuditForUpdate(List<string> sets, DynamicParameters param, Guid uid, DateTime now)
        {
            sets.Add($"[{COL_EDIT_USER}] = @{P_EDIT_USER}"); param.Add(P_EDIT_USER, uid);
            sets.Add($"[{COL_EDIT_TIME}] = @{P_EDIT_TIME}"); param.Add(P_EDIT_TIME, now);
        }
        
        // ----------------- Insert -----------------
        /// <summary>
        /// 新增一筆資料。回傳「受影響筆數」。
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="ct"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<int> InsertAsync<T>(T entity, CancellationToken ct = default)
        {
            var (table, props, rowVersion, _, colByProp) = Reflect<T>();

            var toInsert = props.Where(p => p != rowVersion).ToList();
            var cols = new List<string>(toInsert.Select(p => $"[{colByProp[p.Name]}]"));
            var vals = new List<string>(toInsert.Select(p => "@" + p.Name));

            // DynamicParameters，把實體 + 審計參數一起帶
            var dp = new DynamicParameters(entity);

            // 如果選擇加上五個資料庫預設欄位
            if (EnableAuditColumns)
            {
                var id = CurrentUserIdOrEmpty();
                var now = await GetDbUtcNowAsync(ct);
                AppendAuditForInsert(cols, vals, dp, id, now);
            }

            var sql = $"INSERT INTO {table} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)});";
            return await _db.ExecuteAsync(sql, dp, ct: ct);
        }

        /// <summary>
        /// Insert 並回傳自增 ID（目前 Guid目前都server端生 還沒用到）
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="ct"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<long?> InsertAndGetIdentityAsync<T>(T entity, CancellationToken ct = default)
        {
            var (table, props, rowVersion, _, colByProp) = Reflect<T>();

            var toInsert = props.Where(p => p != rowVersion).ToList();
            var cols = new List<string>(toInsert.Select(p => $"[{colByProp[p.Name]}]"));
            var vals = new List<string>(toInsert.Select(p => "@" + p.Name));
            var dp = new DynamicParameters(entity);

            if (EnableAuditColumns)
            {
                var uid = CurrentUserIdOrEmpty();
                var now = await GetDbUtcNowAsync(ct);
                AppendAuditForInsert(cols, vals, dp, uid, now);
            }

            var sql = $"INSERT INTO {table} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)}); SELECT CAST(SCOPE_IDENTITY() AS bigint);";
            return await _db.ExecuteScalarAsync<long?>(sql, dp, ct: ct);
        }

        // ------------- Update（By 主鍵，整欄更新） -------------
        /// <summary>
        /// 依主鍵更新整筆資料。
        /// - IncludeNulls：null 也寫回（整張表單提交）
        /// - IgnoreNulls：只更新非 null 欄位（避免覆蓋 DB 的值）
        /// - 若有 [Timestamp]，會用它做樂觀鎖（WHERE 帶 RowVersion），被別人改過會回 0
        /// 目前還沒有實作 RowVersion 樂觀鎖
        /// </summary>
        public async Task<int> UpdateAllByIdAsync<T>(T entity, UpdateNullBehavior mode = UpdateNullBehavior.IncludeNulls, CancellationToken ct = default)
        {
            var (table, props, rowVersion, key, colByProp) = Reflect<T>();

            var updatable = props.Where(p => p != key && p != rowVersion).ToList();
            if (mode == UpdateNullBehavior.IgnoreNulls)
            {
                updatable = updatable.Where(p => p.GetValue(entity) != null).ToList();
            }

            if (updatable.Count == 0)
            {
                throw new ArgumentException("沒有可更新的欄位（可能全部是 null 且使用 IgnoreNulls）");
            }

            var sets = new List<string>(updatable.Select(p => $"[{colByProp[p.Name]}] = @{p.Name}"));
            var dp = new DynamicParameters(entity);

            if (EnableAuditColumns)
            {
                var uid = CurrentUserIdOrEmpty();
                var now = await GetDbUtcNowAsync(ct);
                AppendAuditForUpdate(sets, dp, uid, now);
            }

            var where = $"[{colByProp[key.Name]}] = @{key.Name}";
            if (rowVersion != null)
            {
                where += $" AND [{colByProp[rowVersion.Name]}] = @{rowVersion.Name}";
            }

            var sql = $"UPDATE {table} SET {string.Join(", ", sets)} WHERE {where};";
            return await _db.ExecuteAsync(sql, dp, ct: ct);
        }

        // ------------- Select / Delete（By WHERE） -------------
        /// <summary>
        /// 回傳 List
        /// </summary>
        public async Task<List<T>> SelectAsync<T>(CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            // var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}]"));
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            var sql = $"SELECT {cols} FROM {table};";
            return await _db.QueryAsync<T>(sql, ct: ct);
        }
        
        /// <summary>
        /// 依條件查詢（回傳 List）
        /// </summary>
        public async Task<List<T>> SelectWhereAsync<T>(WhereBuilder<T> where, CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));
            var (whereSql, param) = where.Build();
            var sql = $"SELECT {cols} FROM {table} {whereSql};";
            return await _db.QueryAsync<T>(sql, param, ct: ct);
        }

        /// <summary>
        /// 根據 Where 條件取得第一筆
        /// </summary>
        /// <param name="where"></param>
        /// <param name="ct"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<T?> SelectFirstOrDefaultAsync<T>(
            WhereBuilder<T> where,
            CancellationToken ct = default)
        {
            var (table, props, _, _, colByProp) = Reflect<T>();
            var cols = string.Join(", ", props.Select(p => $"[{colByProp[p.Name]}] AS [{p.Name}]"));

            var (whereSql, param) = where.Build();
            var sql = $"SELECT TOP (1) {cols} FROM {table} {whereSql};";
            return await _db.QueryFirstOrDefaultAsync<T>(sql, param, ct: ct);
        }
        
        /// <summary>
        /// 依條件刪除（請務必至少一個條件，程式已防呆）
        /// </summary>
        public async Task<int> DeleteWhereAsync<T>(WhereBuilder<T> where, CancellationToken ct = default)
        {
            var (table, _, _, _, _) = Reflect<T>();
            var (whereSql, param) = where.Build();
            var sql = $"UPDATE {table} SET IS_DELETE = 1 {whereSql};"; 
            return await _db.ExecuteAsync(sql, param, ct: ct);
        }

        // ------------- 反射工具 -------------
        /// <summary>
        /// 讀出：完整表名（含 schema）、全部屬性、RowVersion 屬性、Key 屬性、欄位對照表
        /// 只看 4 個屬性：[Table]、[Column]、[Key]、[Timestamp]
        /// </summary>
        internal static (string table,
                         List<PropertyInfo> props,
                         PropertyInfo? rowVersion,
                         PropertyInfo key,
                         Dictionary<string, string> colByProp) Reflect<T>()
        {
            var t = typeof(T);

            // 表名：支援 [Table(Name="...", Schema="...")]；沒標就用類名
            var tableAttr = t.GetCustomAttribute<TableAttribute>();
            var rawName = tableAttr?.Name ?? t.Name;
            var schema = tableAttr?.Schema;

            // 用 [] 保護避免關鍵字衝突
            string Quote(string s) => $"[{s}]";
            var tableFull = schema is null ? Quote(rawName) : $"{Quote(schema)}.{Quote(rawName)}";

            // 全部 public instance 屬性
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();

            // 主鍵一定要有
            var key = props.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null)
                      ?? throw new InvalidOperationException($"{t.Name} 缺少 [Key] 屬性。");

            // RowVersion 可有可無
            var rowVersion = props.FirstOrDefault(p => p.GetCustomAttribute<TimestampAttribute>() != null);

            // 欄位名對照：優先 [Column(Name="...")]，否則用屬性名
            var colByProp = props.ToDictionary(
                p => p.Name,
                p => p.GetCustomAttribute<ColumnAttribute>()?.Name ?? p.Name,
                StringComparer.OrdinalIgnoreCase);

            return (tableFull, props, rowVersion, key, colByProp);
        }
        
        // ================== 私有：審計蓋章工具 ==================
        private void ApplyAuditOnInsert<T>(T entity, List<PropertyInfo> props, Guid id, DateTime now)
        {
            var map = props.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            SetIfExists(map, entity, "CreateUser", id);
            SetIfExists(map, entity, "CreateTime", now);
            SetIfExists(map, entity, "EditUser", id);
            SetIfExists(map, entity, "EditTime", now);

            // 預設未刪除
            SetIfExists(map, entity, "IsDelete", false);
        }
        
        private static void SetIfExists<TVal>(Dictionary<string, PropertyInfo> map, object entity, string propName, TVal value)
        {
            if (!map.TryGetValue(propName, out var p)) return;
            if (!p.CanWrite) return;

            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            try
            {
                object? v = value;
                // Guid -> Guid?、DateTime -> DateTime? 等兼容
                if (t.IsEnum)
                    v = Enum.ToObject(t, v!);
                else if (t != value?.GetType())
                    v = Convert.ChangeType(v, t);

                p.SetValue(entity, v);
            }
            catch
            {
                // 型別不合就略過（避免拋例外中斷主要流程）
            }
        }
    }
    
    /// <summary>
    /// WHERE 組裝器（只做 AND）
    /// 重點：
    /// 1) 欄位用 Lambda（x => x.Prop）避免魔法字串
    /// 2) 值一律參數化，避免 SQL Injection
    /// 3) AndEq(null) 會自動變成 IS NULL
    /// </summary>
    public sealed class WhereBuilder<T>
    {
        private readonly List<string> _clauses = new();
        private readonly DynamicParameters _param = new();
        private int _idx = 0; // 讓參數名不重複

        public WhereBuilder<T> AndEq(Expression<Func<T, object>> field, object? value)
        {
            var col = GetColumnName(field);
            if (value is null)
            {
                _clauses.Add($"[{col}] IS NULL");
            }
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

        public WhereBuilder<T> AndIn<TValue>(Expression<Func<T, object>> field, IEnumerable<TValue> values)
        {
            var col = GetColumnName(field);
            var p = Next(col);
            _clauses.Add($"[{col}] IN @{p}"); // Dapper 會展開 IEnumerable
            _param.Add(p, values);
            return this;
        }

        public WhereBuilder<T> AndGt(Expression<Func<T, object>> field, object value)  => Add(field, ">",  value);
        public WhereBuilder<T> AndGte(Expression<Func<T, object>> field, object value) => Add(field, ">=", value);
        public WhereBuilder<T> AndLt(Expression<Func<T, object>> field, object value)  => Add(field, "<",  value);
        public WhereBuilder<T> AndLte(Expression<Func<T, object>> field, object value) => Add(field, "<=", value);

        public WhereBuilder<T> AndBetween(Expression<Func<T, object>> field, object from, object to)
        {
            var col = GetColumnName(field);
            var p1 = Next(col + "_from");
            var p2 = Next(col + "_to");
            _clauses.Add($"[{col}] BETWEEN @{p1} AND @{p2}");
            _param.Add(p1, from);
            _param.Add(p2, to);
            return this;
        }

        /// <summary>
        /// 加入 IS_DELETE = 0 條件（常用於軟刪除邏輯）
        /// </summary>
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
            if (_clauses.Count == 0)
                throw new InvalidOperationException("WHERE 需要至少一個條件（避免誤刪/誤更整張表）");
            return ("WHERE " + string.Join(" AND ", _clauses), _param);
        }

        // ---- 小工具 ----

        private WhereBuilder<T> Add(Expression<Func<T, object>> field, string op, object value)
        {
            var col = GetColumnName(field);
            var p = Next(col);
            _clauses.Add($"[{col}] {op} @{p}");
            _param.Add(p, value);
            return this;
        }

        // 從 x => x.Prop 取出字串 "Prop"
        private static string GetPropertyName(Expression<Func<T, object>> selector)
        {
            return selector.Body switch
            {
                MemberExpression me => me.Member.Name,
                UnaryExpression { Operand: MemberExpression me2 } => me2.Member.Name, // 例如值型別被裝箱
                _ => throw new ArgumentException("只支援 x => x.Prop 的寫法")
            };
        }

        // 把 Prop 名字轉成真正的資料庫欄位名（尊重 [Column]，沒寫就用屬性名）
        private static string GetColumnName(Expression<Func<T, object>> selector)
        {
            var propName = GetPropertyName(selector);
            var (table, props, _, _, colByProp) = SQLGenerateHelper.Reflect<T>();
            var prop = props.FirstOrDefault(p => p.Name == propName)
                       ?? throw new ArgumentException($"找不到屬性：{propName}");
            return colByProp[prop.Name];
        }

        private string Next(string baseName) => $"{baseName}_{_idx++}";
    }
}
