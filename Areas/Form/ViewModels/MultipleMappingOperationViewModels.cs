using System;
using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 多對多設定檔摘要資訊，提供前端呈現可供選擇的設定清單。
/// </summary>
public class MultipleMappingConfigViewModel
{
    /// <summary>
    /// FORM_FIELD_Master 的唯一識別碼。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 設定檔顯示名稱。
    /// </summary>
    public string FormName { get; set; } = string.Empty;

    /// <summary>
    /// 主表名稱。
    /// </summary>
    public string BaseTableName { get; set; } = string.Empty;

    /// <summary>
    /// 明細表名稱。
    /// </summary>
    public string DetailTableName { get; set; } = string.Empty;

    /// <summary>
    /// 關聯表名稱。
    /// </summary>
    public string MappingTableName { get; set; } = string.Empty;

    /// <summary>
    /// 關聯表指向主表的外鍵欄位名稱。
    /// </summary>
    public string MappingBaseFkColumn { get; set; } = string.Empty;

    /// <summary>
    /// 關聯表指向明細表的外鍵欄位名稱。
    /// </summary>
    public string MappingDetailFkColumn { get; set; } = string.Empty;
}

/// <summary>
/// 左右清單的單筆項目，包含主鍵值與欄位資料。
/// </summary>
public class MultipleMappingItemViewModel
{
    /// <summary>
    /// 明細資料的主鍵值（字串化）。
    /// </summary>
    public string DetailPk { get; set; } = string.Empty;

    /// <summary>
    /// 明細資料的欄位與目前值。
    /// </summary>
    public Dictionary<string, object?> Fields { get; set; } = new();
}

/// <summary>
/// 左右清單回傳模型，提供已關聯與未關聯的資料集合。
/// </summary>
public class MultipleMappingListViewModel
{
    /// <summary>
    /// 設定檔識別碼。
    /// </summary>
    public Guid FormMasterId { get; set; }

    /// <summary>
    /// 主表主鍵欄位名稱，方便前端標示當前 Base 篩選條件。
    /// </summary>
    public string BasePkColumn { get; set; } = string.Empty;

    /// <summary>
    /// 使用者查詢的主表主鍵值。
    /// </summary>
    public string BasePk { get; set; } = string.Empty;

    /// <summary>
    /// 明細表的主鍵欄位名稱。
    /// </summary>
    public string DetailPkColumn { get; set; } = string.Empty;

    /// <summary>
    /// 關聯表指向主表的外鍵欄位名稱。
    /// </summary>
    public string MappingBaseFkColumn { get; set; } = string.Empty;

    /// <summary>
    /// 關聯表指向明細表的外鍵欄位名稱。
    /// </summary>
    public string MappingDetailFkColumn { get; set; } = string.Empty;

    /// <summary>
    /// 已建立對應關係的明細資料清單（左側）。
    /// </summary>
    public List<MultipleMappingItemViewModel> Linked { get; set; } = new();

    /// <summary>
    /// 尚未建立對應關係的明細資料清單（右側）。
    /// </summary>
    public List<MultipleMappingItemViewModel> Unlinked { get; set; } = new();
}

/// <summary>
/// 批次建立或刪除關聯的輸入模型。
/// </summary>
public class MultipleMappingUpsertViewModel
{
    /// <summary>
    /// 目標主檔的主鍵值。
    /// </summary>
    public string BaseId { get; set; } = string.Empty;

    /// <summary>
    /// 需要建立或移除的明細主鍵清單。
    /// </summary>
    public List<string> DetailIds { get; set; } = new();
}
