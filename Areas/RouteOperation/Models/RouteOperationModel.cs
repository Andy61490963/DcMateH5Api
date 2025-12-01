using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.RouteOperation.Models;


[Table("BAS_ROUTE")]
public class BAS_ROUTE
{
    [Key]
    public decimal SID { get; set; }
    public string ROUTE_CODE { get; set; } = string.Empty;
    public string ROUTE_NAME { get; set; } = string.Empty;
}

[Table("BAS_OPERATION")]
public class BAS_OPERATION
{
    [Key]
    public decimal SID { get; set; }
    public string OPERATION_TYPE { get; set; } = string.Empty;
    public string OPERATION_CODE { get; set; } = string.Empty;
    public string OPERATION_NAME { get; set; } = string.Empty;
}

[Table("BAS_ROUTE_OPERATION")]
public class BAS_ROUTE_OPERATION
{
    [Key]
    public decimal SID { get; set; }
    public decimal BAS_ROUTE_SID { get; set; }
    public decimal BAS_OPERATION_SID { get; set; }
    public int SEQ { get; set; }
    public string? ERP_STAGE { get; set; }
    public bool END_FLAG { get; set; }
}

[Table("BAS_ROUTE_OPERATION_CONDITION")]
public class BAS_ROUTE_OPERATION_CONDITION
{
    [Key]
    public decimal SID { get; set; }
    public decimal BAS_ROUTE_OPERATION_SID { get; set; }
    public decimal BAS_CONDITION_SID { get; set; }
    public decimal? NEXT_ROUTE_OPERATION_SID { get; set; }
    public decimal? NEXT_ROUTE_EXTRA_OPERATION_SID { get; set; }
    public string HOLD { get; set; } = "N";
    public int? SEQ { get; set; }
}

[Table("BAS_ROUTE_OPERATION_EXTRA")]
public class BAS_ROUTE_OPERATION_EXTRA
{
    [Key]
    public decimal SID { get; set; }
    public decimal BAS_ROUTE_SID { get; set; }
    public decimal BAS_OPERATION_SID { get; set; }
}

[Table("BAS_CONDITION")]
public class BAS_CONDITION
{
    [Key]
    public decimal SID { get; set; }
    public string CONDITION_CODE { get; set; } = string.Empty;
    public string LEFT_EXPRESSION { get; set; } = string.Empty;
    public string OPERATOR { get; set; } = string.Empty;
    public string RIGHT_VALUE { get; set; } = string.Empty;
}