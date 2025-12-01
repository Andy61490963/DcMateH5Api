namespace DcMateH5Api.Areas.RouteOperation.ViewModels;

/// <summary>
/// 建立 Route 工作站節點的 Request。
/// 對應 BAS_ROUTE_OPERATION。
/// </summary>
public class CreateRouteOperationRequest
{
    /// <summary>Route 主鍵 (BAS_ROUTE_SID)，由 Controller route 確認一致性。</summary>
    public decimal RouteSid { get; set; }

    /// <summary>對應 BAS_OPERATION.SID。</summary>
    public decimal OperationSid { get; set; }

    /// <summary>流程順序（同 Route 內不可重複）。</summary>
    public int Seq { get; set; }

    /// <summary>ERP 對應站別註記，可為 null。</summary>
    public string? ErpStage { get; set; }

    /// <summary>是否為最後一站。</summary>
    public bool EndFlag { get; set; }
}

/// <summary>
/// 更新 Route 工作站節點的 Request。
/// 可以部分更新，因此多數屬性為 nullable。
/// </summary>
public class UpdateRouteOperationRequest
{
    /// <summary>流程順序（同 Route 內不可重複）。</summary>
    public int? Seq { get; set; }

    /// <summary>ERP 對應站別註記，可為 null。</summary>
    public string? ErpStage { get; set; }

    /// <summary>是否為最後一站。</summary>
    public bool? EndFlag { get; set; }
}

/// <summary>
/// 建立 RouteOperationCondition 的 Request。
/// 對應 BAS_ROUTE_OPERATION_CONDITION。
/// </summary>
public class CreateRouteOperationConditionRequest
{
    /// <summary>父節點 RouteOperation 的 SID（BAS_ROUTE_OPERATION_SID）。</summary>
    public decimal RouteOperationSid { get; set; }

    /// <summary>條件定義主鍵（BAS_CONDITION_SID）。</summary>
    public decimal ConditionSid { get; set; }

    /// <summary>條件判斷順序，同一個 RouteOperation 下不可重複。</summary>
    public int Seq { get; set; }

    /// <summary>條件成立時的下一個主線站別（BAS_ROUTE_OPERATION.SID）。</summary>
    public decimal? NextRouteOperationSid { get; set; }

    /// <summary>條件成立時要插入的額外站別（BAS_ROUTE_OPERATION_EXTRA.SID）。</summary>
    public decimal? NextRouteExtraOperationSid { get; set; }

    /// <summary>是否 HOLD：'Y' 或 'N'。</summary>
    public string Hold { get; set; } = "N";
}

/// <summary>
/// 更新 RouteOperationCondition 的 Request。
/// </summary>
public class UpdateRouteOperationConditionRequest
{
    /// <summary>條件順序，可選更新。</summary>
    public int? Seq { get; set; }

    /// <summary>條件定義主鍵（如需要可以允許調整）。</summary>
    public decimal? ConditionSid { get; set; }

    /// <summary>條件成立時的下一個主線站別。</summary>
    public decimal? NextRouteOperationSid { get; set; }

    /// <summary>條件成立時要插入的額外站別。</summary>
    public decimal? NextRouteExtraOperationSid { get; set; }

    /// <summary>是否 HOLD，'Y' / 'N'。</summary>
    public string? Hold { get; set; }
}