using System.Threading;
using System.Threading.Tasks;

namespace DcMateH5Api.Logging;

/// <summary>
/// Abstraction for writing SQL execution logs.
/// </summary>
public interface ISqlLogService
{
    /// <summary>
    /// Persists a log entry describing one executed SQL command.
    /// </summary>
    Task LogAsync(SqlLogEntry entry, CancellationToken ct = default);
}
