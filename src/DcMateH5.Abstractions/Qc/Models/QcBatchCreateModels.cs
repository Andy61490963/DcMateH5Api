namespace DcMateH5.Abstractions.Qc.Models;

public class QcBatchCreateRequest
{
    public List<QcHeaderCreateRequest> HEADERS { get; set; } = new();
}

public class QcHeaderCreateRequest
{
    public string INSPECTION_NO { get; set; } = string.Empty;
    public string INSPECTION_TYPE { get; set; } = string.Empty;
    public string? MATERIAL_CHECK { get; set; }
    public string? CHECK_RESULT { get; set; }
    public decimal? STANDARD_WEIGHT { get; set; }
    public decimal? SAMPLING_QTY { get; set; }
    public int? CAVITY { get; set; }
    public string? MOLD_NO { get; set; }
    public string? MOLD_TOL_NO { get; set; }
    public string? EQP_NO { get; set; }
    public decimal? WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public string? SOURCE_NO { get; set; }
    public string WORK_ORDER { get; set; } = string.Empty;
    public string ITEM_NO { get; set; } = string.Empty;
    public DateTime INSPECTION_TIME { get; set; }
    public string INSPECTION_RESULT { get; set; } = string.Empty;
    public string INSPECTOR { get; set; } = string.Empty;
    public string? APPROVE_USER { get; set; }
    public string? PRODUCTION_STATUS { get; set; }
    public string? COMMENT { get; set; }
    public string? CREATE_USER { get; set; }
    public string? EDIT_USER { get; set; }
    public List<QcDetailCreateRequest> DETAILS { get; set; } = new();
}

public class QcDetailCreateRequest
{
    public string? INSPECTION_NO { get; set; }
    public string ITEM_NO { get; set; } = string.Empty;
    public string INSPECTION_ITEM { get; set; } = string.Empty;
    public string? INSPECTION_VALUE { get; set; }
    public int INSPECTION_TIME_MINUTES { get; set; }
    public string? USL { get; set; }
    public string? UCL { get; set; }
    public int? SAMPLE_SIZE { get; set; }
    public string? TARGET { get; set; }
    public string? LCL { get; set; }
    public string? LSL { get; set; }
    public int? BASE_WORK_TIME { get; set; }
    public int? ROW_COUNT { get; set; }
    public string? CREATE_USER { get; set; }
    public string? EDIT_USER { get; set; }
}

public class QcBatchCreateResponse
{
    public List<string> INSPECTION_NOS { get; set; } = new();
}

public class QcErrorResponse
{
    public string Message { get; set; } = string.Empty;
}
