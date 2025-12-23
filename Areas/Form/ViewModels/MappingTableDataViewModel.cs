using System;
using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 提供多對多關聯表的完整資料列清單，包含欄位名稱與對應值。
/// </summary>
public class MappingTableDataViewModel
{
    /// <summary>
    /// <para>輸入的 FormMasterId，對應 FORM_FIELD_MASTER.MAPPING_TABLE_ID。</para>
    /// <para>供回傳時檢核參數與來源資料一致性。</para>
    /// </summary>
    public Guid FormMasterId { get; set; }

    /// <summary>
    /// <para>實際查詢到的關聯表名稱。</para>
    /// <para>利於前端或記錄層追蹤此次查詢來源。</para>
    /// </summary>
    public string MappingTableName { get; set; } = string.Empty;

    /// <summary>
    /// <para>對應表的key值。</para>
    /// </summary>
    public string MappingTableKey { get; set; } = string.Empty;
    
    /// <summary>
    /// <para>該關聯表的所有資料列。</para>
    /// <para>每筆資料列均包含「欄位名稱 / 欄位值」的結構化對應。</para>
    /// </summary>
    public List<MappingTableRowViewModel> Rows { get; set; } = new();
}

/// <summary>
/// 關聯表的單筆資料列模型，使用明確的字典保存欄位與值，避免 dynamic 帶來的隱藏問題。
/// </summary>
public class MappingTableRowViewModel
{
    /// <summary>
    /// <para>欄位名稱對應欄位值的集合。</para>
    /// <para>採用大小寫不敏感的字典以提高容錯與查詢便利性。</para>
    /// </summary>
    public Dictionary<string, object?> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
