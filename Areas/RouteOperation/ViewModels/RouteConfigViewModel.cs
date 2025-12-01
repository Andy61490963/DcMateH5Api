namespace DcMateH5Api.Areas.RouteOperation.ViewModels
{
    /// <summary>
    /// 單一 Route 的完整組態（主線站別 + 條件 + Extra 站）。
    /// </summary>
    public class RouteConfigViewModel
    {
        public decimal RouteSid { get; set; }
        public string RouteCode { get; set; } = string.Empty;
        public string RouteName { get; set; } = string.Empty;

        public List<RouteOperationDetailViewModel> Operations { get; set; } = new();
        public List<RouteExtraOperationViewModel> ExtraOperations { get; set; } = new();
    }

    /// <summary>
    /// Route 上的單一工作站節點資訊。
    /// 對應 BAS_ROUTE_OPERATION + BAS_OPERATION。
    /// </summary>
    public class RouteOperationDetailViewModel
    {
        public decimal RouteOperationSid { get; set; }
        public decimal RouteSid { get; set; }
        public decimal OperationSid { get; set; }

        public int Seq { get; set; }
        public string? ErpStage { get; set; }
        public bool EndFlag { get; set; }

        public string OperationCode { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;

        public List<RouteOperationConditionViewModel> Conditions { get; set; } = new();
    }

    /// <summary>
    /// Extra Route 上的單一工作站節點資訊。
    /// 對應 BAS_ROUTE_OPERATION + BAS_OPERATION。
    /// </summary>
    public class RouteExtraOperationDetailViewModel
    {
        public decimal RouteOperationSid { get; set; }
        public decimal RouteSid { get; set; }
        public decimal OperationSid { get; set; }

        public string OperationCode { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;

        public List<RouteOperationConditionViewModel> Conditions { get; set; } = new();
    }
    
    /// <summary>
    /// 附掛在 Route 上的 Extra Operation。
    /// 對應 BAS_ROUTE_OPERATION_EXTRA + BAS_OPERATION。
    /// </summary>
    public class RouteExtraOperationViewModel
    {
        public decimal RouteExtraSid { get; set; }
        public decimal RouteSid { get; set; }
        public decimal OperationSid { get; set; }

        public string OperationCode { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 綁在某一個 RouteOperation 底下的一筆條件設定。
    /// 對應 BAS_ROUTE_OPERATION_CONDITION + BAS_CONDITION (+ 下一站/Extra 額外資訊)。
    /// </summary>
    public class RouteOperationConditionViewModel
    {
        public decimal ConditionSid { get; set; }
        public decimal RouteOperationSid { get; set; }
        public decimal ConditionDefinitionSid { get; set; }

        public int Seq { get; set; }

        public string ConditionCode { get; set; } = string.Empty;
        public string LeftExpression { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string RightValue { get; set; } = string.Empty;

        public decimal? NextRouteOperationSid { get; set; }
        public decimal? NextRouteExtraOperationSid { get; set; }

        public string Hold { get; set; } = "N";

        // 給前端預覽下一站 / Extra 站資訊用
        public NextOperationInfo? NextOperation { get; set; }
        public NextExtraOperationInfo? NextExtraOperation { get; set; }
    }

    /// <summary>下一主線站別資訊（預覽用）。</summary>
    public class NextOperationInfo
    {
        public decimal RouteOperationSid { get; set; }
        public int Seq { get; set; }

        public string OperationCode { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
    }

    /// <summary>下一個 Extra 站資訊（預覽用）。</summary>
    public class NextExtraOperationInfo
    {
        public decimal RouteExtraSid { get; set; }
        public string OperationCode { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
    }
}
