namespace DcMateH5Api.Areas.RouteOperation.ViewModels;

public class ConditionViewModel
{
    public decimal Sid { get; set; }
    public string ConditionCode { get; set; } = string.Empty;
    public string LeftExpression { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string RightValue { get; set; } = string.Empty;
}

public class CreateConditionRequest
{
    public string ConditionCode { get; set; } = string.Empty;
    public string LeftExpression { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string RightValue { get; set; } = string.Empty;
}

public class UpdateConditionRequest
{
    public string? ConditionCode { get; set; }
    public string? LeftExpression { get; set; }
    public string? Operator { get; set; }
    public string? RightValue { get; set; }
}