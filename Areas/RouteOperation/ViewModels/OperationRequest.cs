namespace DcMateH5Api.Areas.RouteOperation.ViewModels;

public class OperationViewModel
{
    public decimal Sid { get; set; }
    public string OperationType { get; set; } = string.Empty; // Normal / Extra
    public string OperationCode { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
}

public class CreateOperationRequest
{
    /// <summary>Normal / Repair（二選一）</summary>
    public string OperationType { get; set; } = "Normal";
    public string OperationCode { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
}

public class UpdateOperationRequest
{
    public string? OperationType { get; set; }
    public string? OperationCode { get; set; }
    public string? OperationName { get; set; }
}