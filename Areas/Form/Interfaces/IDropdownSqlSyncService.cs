using DcMateH5Api.Areas.Form.Models;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Interfaces;

/// <summary>
/// 提供根據 SQL 內容同步下拉選項的功能，
/// 會確保資料表 <c>FORM_FIELD_DROPDOWN_OPTIONS</c> 與來源資料保持一致。
/// </summary>
public interface IDropdownSqlSyncService
{
    /// <summary>
    /// 執行 SQL 並依據結果同步指定下拉選單的選項。
    /// </summary>
    /// <param name="dropdownId">對應的 <c>FORM_FIELD_DROPDOWN</c> 主鍵。</param>
    /// <param name="sql">已驗證的 SQL 查詢語句。</param>
    /// <param name="transaction">可選，沿用既有交易以確保一致性。</param>
    /// <returns>同步後的結果摘要。</returns>
    DropdownSqlSyncResult Sync(Guid dropdownId, string sql, SqlTransaction? transaction = null);
}
