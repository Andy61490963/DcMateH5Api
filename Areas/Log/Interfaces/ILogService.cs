

using DcMateH5Api.Areas.Log.Models;

namespace DcMateH5Api.Areas.Log.Interfaces
{
    /// <summary>
    /// 提供寫入 SQL 執行記錄的服務介面。
    /// </summary>
    public interface ILogService
    {
        /// <summary>
        /// 寫入一筆 SQL 執行紀錄。
        /// </summary>
        /// <param name="entry">待寫入的紀錄。</param>
        /// <param name="ct">取消權杖。</param>
        Task LogAsync(SqlLogEntry entry, CancellationToken ct = default);
        
        /// <summary>
        /// 取得 LOG 資訊
        /// </summary>
        /// <param name="query"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<IReadOnlyList<SqlLogEntry>> GetLogsAsync(
            SqlLogQuery query,
            CancellationToken ct = default);
    }
    
}
