using System;
using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 提供明細列資料給前端顯示或轉移使用。
/// </summary>
public class FormDetailRowViewModel
{
    /// <summary>明細資料主鍵。</summary>
    public string? Pk { get; set; }

    /// <summary>欄位與值的對應清單。</summary>
    public List<FormInputField> Fields { get; set; } = new();

    /// <summary>原始資料庫欄位值對應表。</summary>
    public Dictionary<string, object?> RawData { get; set; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
