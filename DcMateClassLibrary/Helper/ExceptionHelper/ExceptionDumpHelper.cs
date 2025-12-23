using System.Collections;
using Microsoft.Data.SqlClient;
using System.Reflection;

public static class ExceptionDumpHelper
{
    /// <summary>
    /// 將 Exception 轉成「可 JSON 序列化」的 dump 物件
    /// - 目的：快速定位問題（包含 stack trace / inner exception / sql details）
    /// - 注意：這會暴露大量細節，建議僅用於內網或 debug 情境
    /// </summary>
    public static object BuildExceptionDump(Exception ex, int maxDepth = 10)
    {
        // visited 用來避免 InnerException 循環
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        return BuildExceptionDumpInternal(ex, depth: 0, maxDepth, visited);
    }

    private static object BuildExceptionDumpInternal(
        Exception ex,
        int depth,
        int maxDepth,
        HashSet<Exception> visited)
    {
        if (depth >= maxDepth)
        {
            return new
            {
                ExceptionType = ex.GetType().FullName,
                ex.Message,
                Note = $"Max depth {maxDepth} reached"
            };
        }

        // 循環偵測：避免無限遞迴
        if (!visited.Add(ex))
        {
            return new
            {
                ExceptionType = ex.GetType().FullName,
                ex.Message,
                Note = "Cyclic InnerException detected"
            };
        }

        // SqlException 額外資訊
        object? sql = null;
        if (ex is SqlException se)
        {
            sql = new
            {
                se.Number,
                se.State,
                se.Class,
                se.LineNumber,
                se.Procedure,
                se.Server,
                se.ClientConnectionId,
                Errors = se.Errors
                    .Cast<SqlError>()
                    .Select(e => new
                    {
                        e.Number,
                        e.State,
                        e.Class,
                        e.Message,
                        e.Procedure,
                        e.LineNumber,
                        e.Server,
                        e.Source
                    })
                    .ToList()
            };
        }

        // AggregateException：展開所有 InnerExceptions
        object? aggregate = null;
        if (ex is AggregateException ae)
        {
            // Flatten 讓多層 Aggregate 變平面更好看
            var flat = ae.Flatten();
            aggregate = new
            {
                InnerExceptions = flat.InnerExceptions
                    .Select(x => BuildExceptionDumpInternal(x, depth + 1, maxDepth, visited))
                    .ToList()
            };
        }

        // ReflectionTypeLoadException：LoaderExceptions
        object? typeLoad = null;
        if (ex is ReflectionTypeLoadException rtle)
        {
            typeLoad = new
            {
                LoaderExceptions = rtle.LoaderExceptions?
                    .Where(x => x != null)
                    .Select(x => BuildExceptionDumpInternal(x!, depth + 1, maxDepth, visited))
                    .ToList(),
                Types = rtle.Types?
                    .Where(t => t != null)
                    .Select(t => t!.FullName)
                    .ToList()
            };
        }

        // 遞迴 InnerException
        object? inner = null;
        if (ex.InnerException != null)
        {
            inner = BuildExceptionDumpInternal(ex.InnerException, depth + 1, maxDepth, visited);
        }

        // Exception.Data 安全攤平：全部轉字串，避免不可序列化物件
        Dictionary<string, string?>? data = null;
        if (ex.Data.Count > 0)
        {
            data = ex.Data.Cast<DictionaryEntry>()
                .ToDictionary(
                    x => x.Key?.ToString() ?? "",
                    x => x.Value?.ToString());
        }

        return new
        {
            ExceptionType = ex.GetType().FullName,
            ex.Message,
            ex.Source,
            ex.HResult,
            // ToString() 通常會把 type + message + stack trace 組起來，有時比 StackTrace 更完整
            ToString = ex.ToString(),
            StackTrace = ex.StackTrace,
            TargetSite = ex.TargetSite?.ToString(),
            Data = data,

            // 額外區塊（有就帶，沒有就是 null）
            SqlException = sql,
            AggregateException = aggregate,
            ReflectionTypeLoadException = typeLoad,

            InnerException = inner
        };
    }

    /// <summary>
    /// 用「參考相等」比較 Exception，避免同內容不同實體造成誤判
    /// </summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<Exception>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public bool Equals(Exception? x, Exception? y) => ReferenceEquals(x, y);
        public int GetHashCode(Exception obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
