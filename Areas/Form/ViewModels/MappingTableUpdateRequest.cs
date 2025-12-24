using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 更新關聯表資料的請求模型。
/// </summary>
/// <remarks>
/// 業務邏輯：
/// 1. 由控制器接收欄位名稱與對應值。
/// 2. 服務層會驗證欄位合法性，並以參數化 SQL 更新資料。
/// </remarks>
public class MappingTableUpdateRequest
{
    /// <summary>
    /// 欲更新的欄位名稱清單。
    /// </summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>
    /// 對應欄位值清單，順序需與 Columns 一致。
    /// </summary>
    public List<object?> Values { get; set; } = new();
}
