using System;
using System.Collections.Generic;
using System.Threading;
using DcMateH5Api.Areas.Form.ViewModels;

namespace DcMateH5Api.Areas.Form.Interfaces;

/// <summary>
/// 多對多維護的讀寫服務，負責取得設定檔、左右清單與批次關聯操作。
/// </summary>
public interface IFormMultipleMappingService
{
    /// <summary>
    /// 取得所有多對多設定檔清單。
    /// </summary>
    IEnumerable<MultipleMappingConfigViewModel> GetFormMasters(CancellationToken ct = default);

    /// <summary>
    /// 依設定檔與主鍵取得已關聯/未關聯的左右清單。
    /// </summary>
    MultipleMappingListViewModel GetMappingList(Guid formMasterId, string baseId, CancellationToken ct = default);

    /// <summary>
    /// 批次新增對應關係（右 → 左）。
    /// </summary>
    void AddMappings(Guid formMasterId, MultipleMappingUpsertViewModel request, CancellationToken ct = default);

    /// <summary>
    /// 批次移除對應關係（左 → 右）。
    /// </summary>
    void RemoveMappings(Guid formMasterId, MultipleMappingUpsertViewModel request, CancellationToken ct = default);
}
