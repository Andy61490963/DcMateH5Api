using System;
using System.Collections.Generic;
using System.Threading;
using DcMateH5Api.Areas.Form.Models;
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
    /// 取得可進行多對多關聯的主檔資料清單，供前端選擇 Base 主鍵。
    /// </summary>
    List<FormListDataViewModel> GetForms(FormSearchRequest? request = null, CancellationToken ct = default);

    /// <summary>
    /// 依設定檔與主鍵取得已關聯/未關聯的左右清單。
    /// </summary>
    MultipleMappingListViewModel GetMappingList(Guid formMasterId, string baseId, CancellationToken ct = default);

    /// <summary>
    /// 批次新增對應關係（右 → 左）。
    /// </summary>
    void AddMappings(Guid formMasterId, MultipleMappingUpsertViewModel request, bool isSeq, CancellationToken ct = default);

    /// <summary>
    /// 批次移除對應關係（左 → 右）。
    /// </summary>
    void RemoveMappings(Guid formMasterId, MultipleMappingUpsertViewModel request, CancellationToken ct = default);

    /// <summary>
    /// 依指定順序重新整理 Mapping 資料列的 SEQ 欄位，限定於同一個 Base 主鍵範圍內。
    /// </summary>
    /// <param name="request">包含設定檔、排序後 SID 清單與 Base 主鍵值的請求模型。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>更新的筆數。</returns>
    int ReorderMappingSequence(MappingSequenceReorderRequest request, CancellationToken ct = default);

    /// <summary>
    /// 依據 MAPPING_TABLE_ID 取得對應的關聯表所有資料列，並以結構化模型返回。
    /// </summary>
    /// <param name="formMasterId">FORM_FIELD_MASTER.MAPPING_TABLE_ID，指定欲查詢的關聯表來源。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>包含關聯表名稱與完整資料列集合的模型。</returns>
    Task<MappingTableDataViewModel> GetMappingTableData(Guid formMasterId, CancellationToken ct = default);

    /// <summary>
    /// 依 FormMasterId + MappingRowId 更新關聯表指定欄位資料。
    /// </summary>
    /// <remarks>
    /// 核心目標：
    /// 1) 用 MappingRowId 精準定位要更新的那一筆（避免用 formMasterId 硬推 PK）。  
    /// 2) 用 Fields(key:value) 避免 Columns/Values index 對齊風險。  
    /// 3) 白名單欄位 + 參數化 SQL，確保安全與一致性。  
    /// </remarks>
    Task<int> UpdateMappingTableData(Guid formMasterId, MappingTableUpdateRequest request, CancellationToken ct = default);
}
