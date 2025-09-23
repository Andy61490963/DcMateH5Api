using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.Models;

/// <summary>
/// 代表下拉選項同步的結果摘要。
/// </summary>
public sealed class DropdownSqlSyncResult
{
    /// <summary>
    /// 同步後可供前端使用的選項清單。
    /// </summary>
    public List<FormFieldDropdownOptionsDto> Options { get; set; } = new();

    /// <summary>
    /// 供前端預覽的原始查詢結果（最多 10 筆）。
    /// </summary>
    public List<Dictionary<string, object>> PreviewRows { get; set; } = new();

    /// <summary>
    /// SQL 實際回傳的筆數。
    /// </summary>
    public int RowCount { get; set; }
}
