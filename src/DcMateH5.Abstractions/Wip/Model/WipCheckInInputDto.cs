using System.Text.Json.Serialization;

namespace DcMateH5Api.Areas.Wip.Model;

public class WipCheckInInputDto
{
    /// <summary>
    /// ADM_OPI_USER 的 ACCOUNT_NO (複數個，可為 NULL)
    /// </summary>
    public List<string>? Account { get; set; }

    /// <summary>
    /// EQM_MASTER 的 EQM_MASTER_NO (複數個，可為 NULL)
    /// </summary>
    public List<string>? Equipment { get; set; }

    /// <summary>
    /// WIP_WO 的 WO (工單)
    /// </summary>
    public string WorkOrder { get; set; } = null!;

    /// <summary>
    /// 進站時間
    /// </summary>
    public DateTime CheckInTime { get; set; }

    /// <summary>
    /// WIP_OPERATION 的 WIP_OPERATION_NO (工序)
    /// </summary>
    public string Operation { get; set; } = null!;

    /// <summary>
    /// WIP_DEPARTMENT 的 DEPT_NO (部門)
    /// </summary>
    public string Department { get; set; } = null!;

    /// <summary>
    /// 備註 (可為 NULL)
    /// </summary>
    public string? Comment { get; set; }
}

public class WipCheckInCancelInputDto
{
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
}

public class WipCheckInResponseDto
{
    [JsonPropertyName("histSid")]
    public decimal HistSid { get; set; }
}

public class WipModelUploadCheckInInputDto
{
    public List<string>? Account { get; set; }
    public List<string>? Equipment { get; set; }
    public DateTime CheckInTime { get; set; }
    public string Operation { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string? Comment { get; set; }
    public List<WipModelUploadCheckInDetailInputDto>? Details { get; set; } = new();
}

public class WipModelUploadCheckInDetailInputDto
{
    public string WorkOrder { get; set; } = null!;
    public string TolNo { get; set; } = null!;
    public string TolDetalsNo { get; set; } = null!;
    public string PartNo { get; set; } = null!;
    public decimal Cav { get; set; }
}

public class WipModelUploadCheckInResponseDto
{
    [JsonPropertyName("tolSid")]
    public decimal TolSid { get; set; }

    [JsonPropertyName("histSids")]
    public List<decimal> HistSids { get; set; } = new();
}
