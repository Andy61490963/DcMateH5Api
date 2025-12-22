using System;
using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 重新排序多對多 Mapping 資料列 SEQ 的請求模型，封裝表單設定檔與對應 SID 清單。
/// </summary>
public class MappingSequenceReorderRequest
{
    /// <summary>
    /// FORM_FIELD_MASTER 的唯一識別碼。
    /// </summary>
    public Guid FormMasterId { get; set; }

    /// <summary>
    /// 依照最終順序排序好的 Mapping SID 清單，將被更新為 SEQ = 1..N。
    /// </summary>
    public List<decimal> OrderedSids { get; set; } = new();

    /// <summary>
    /// 作用範圍，包含主表主鍵值等上下文資訊。
    /// </summary>
    public MappingSequenceScope Scope { get; set; } = new();
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
