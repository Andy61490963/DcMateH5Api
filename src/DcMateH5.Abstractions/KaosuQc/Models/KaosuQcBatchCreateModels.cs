namespace DcMateH5.Abstractions.KaosuQc.Models;

/// <summary>
/// Kaosu 品檢批次新增請求。
/// </summary>
public class KaosuQcBatchCreateRequest
{
    /// <summary>
    /// 批次單頭資料集合。
    /// </summary>
    public List<KaosuQcHeaderCreateRequest> Headers { get; set; } = new();
}

/// <summary>
/// Kaosu 品檢單頭新增模型。
/// </summary>
public class KaosuQcHeaderCreateRequest
{
    /// <summary>檢驗單號（唯一值）。</summary>
    public string InspectionNo { get; set; } = string.Empty;

    /// <summary>檢驗型別。</summary>
    public string? InspectionType { get; set; }

    // 2026/02/24 新增 2 欄位
    public string? MaterialCheck { get; set; }
    
    public string? CheckResult { get; set; }
    
    // 2026/02/25 新增 4 欄位
    public decimal? StandardWeight { get; set; }
    
    public int? Cavity { get; set; }
    
    public string? MoldNo { get; set; }
    
    public string? EqpNo { get; set; }
    
    
    
    /// <summary>來源單號。</summary>
    public string? SourceNo { get; set; }

    /// <summary>工單號碼。</summary>
    public string? WorkOrder { get; set; }

    /// <summary>治具代碼。</summary>
    public string? ItemNo { get; set; }

    /// <summary>檢驗時間。</summary>
    public DateTime? InspectionTime { get; set; }

    /// <summary>檢驗結果。</summary>
    public string? InspectionResult { get; set; }

    /// <summary>檢驗員。</summary>
    public string? Inspector { get; set; }

    public string? Comment { get; set; }

    /// <summary>建立者（可不帶；將由 CurrentUserAccessor 補值）。</summary>
    public string? CreatedUser { get; set; }

    /// <summary>異動者（可不帶；將由 CurrentUserAccessor 補值）。</summary>
    public string? EditUser { get; set; }

    /// <summary>單身資料集合。</summary>
    public List<KaosuQcDetailCreateRequest> Details { get; set; } = new();
}

/// <summary>
/// Kaosu 品檢單身新增模型。
/// </summary>
public class KaosuQcDetailCreateRequest
{
    /// <summary>檢驗單號（若與單頭不同，將以單頭覆蓋）。</summary>
    public string? InspectionNo { get; set; }

    /// <summary>料號。</summary>
    public string? ItemNo { get; set; }

    /// <summary>檢驗項目。</summary>
    public string? InspectionItem { get; set; }

    /// <summary>檢驗值。</summary>
    public string? InspectionValue { get; set; }

    /// <summary>檢驗時間(分鐘)。</summary>
    public int? InspectionTimeMinutes { get; set; }
    
    // 2026/02/24 新增 7 欄位
    public string? Usl { get; set; }
    public string? Ucl { get; set; }
    public int? SampleSize { get; set; }
    public string? Target { get; set; }
    public string? Lcl { get; set; }
    public string? Lsl { get; set; }
    public int? BaseWorkTime { get; set; }
    
    // 2026/02/24 新增 7 欄位
    public int? RowCount { get; set; }
    
    /// <summary>建立者（可不帶；將由單頭/CurrentUserAccessor 補值）。</summary>
    public string? CreatedUser { get; set; }

    /// <summary>異動者（可不帶；將由單頭/CurrentUserAccessor 補值）。</summary>
    public string? EditUser { get; set; }
}

/// <summary>
/// 批次新增成功回傳模型。
/// </summary>
public class KaosuQcBatchCreateResponse
{
    /// <summary>
    /// 成功建立之檢驗單號清單。
    /// </summary>
    public List<string> InspectionNos { get; set; } = new();
}

/// <summary>
/// API 失敗回傳模型（避免直接曝露敏感錯誤資訊）。
/// </summary>
public class KaosuQcErrorResponse
{
    /// <summary>
    /// 可對外顯示之錯誤訊息。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
