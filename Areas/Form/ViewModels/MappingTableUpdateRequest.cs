using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.ViewModels;

public sealed class MappingTableUpdateRequest
{
    /// <summary>
    /// 欲更新的資料列主鍵值（字串型別收，避免 PK 是 int/decimal/uniqueidentifier 時卡死）
    /// </summary>
    public string MappingRowId { get; set; } = string.Empty;

    /// <summary>
    /// 欄位更新集合（Key = 欄位名稱, Value = 欲更新的值）
    /// </summary>
    public Dictionary<string, object?> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
