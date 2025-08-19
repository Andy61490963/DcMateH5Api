using System.Threading;
using System.Threading.Tasks;

namespace DcMateH5Api.Logging;

/// <summary>
/// Provides functionality to persist SQL execution logs.
/// </summary>
public interface ISqlLogService
{
    /// <summary>
    /// Persists a SQL execution log entry.
    /// </summary>
    /// <param name="entry">The log entry.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LogAsync(SqlLogEntry entry, CancellationToken ct = default);
}

/// <summary>
/// Simple no-op implementation used when a concrete storage is not required.
/// </summary>
public sealed class SqlLogService : ISqlLogService
{
    public Task LogAsync(SqlLogEntry entry, CancellationToken ct = default) => Task.CompletedTask;
}

