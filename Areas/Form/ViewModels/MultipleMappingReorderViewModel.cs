using System;
using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 重新排序多對多 Mapping 資料列 SEQ 的請求模型，封裝表單設定檔與對應 SID 清單。
/// </summary>
public class MappingSequenceReorderRequest
{
    public Guid FormMasterId { get; set; }
    public MappingSequenceScope Scope { get; set; } = default!;

    /// <summary>
    /// 依照前端排序後的「Mapping PK」字串清單（對應 header.MAPPING_PK_COLUMN）
    /// </summary>
    public List<string> OrderedIds { get; set; } = new();
}

/// <summary>
/// 重新排序動作的範圍設定。
/// </summary>
public class MappingSequenceScope
{
    /// <summary>
    /// 多對多設定檔所屬主表的主鍵值，用於限定 Mapping 表資料列。
    /// </summary>
    public string BasePkValue { get; set; } = string.Empty;
}
