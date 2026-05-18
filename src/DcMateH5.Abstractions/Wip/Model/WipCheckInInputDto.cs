using System.Text.Json.Serialization;

namespace DcMateH5Api.Areas.Wip.Model;

public class WipCheckInInputDto
{
    /// <summary>
    /// ADM_OPI_USER 的操作人員帳號清單。
    /// </summary>
    public List<string>? Account { get; set; }

    /// <summary>
    /// EQM_MASTER 的機台編號清單。
    /// </summary>
    public List<string>? Equipment { get; set; }

    /// <summary>
    /// WIP_WO 的工單編號。
    /// </summary>
    public string WorkOrder { get; set; } = null!;

    /// <summary>
    /// 工單進站時間。
    /// </summary>
    public DateTime CheckInTime { get; set; }

    /// <summary>
    /// WIP_OPERATION 的工序編號。
    /// </summary>
    public string Operation { get; set; } = null!;

    /// <summary>
    /// WIP_DEPARTMENT 的部門編號。
    /// </summary>
    public string Department { get; set; } = null!;

    /// <summary>
    /// 選填的進站備註。
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

/// <summary>
/// 上模開始進站資料。
/// </summary>
public class WipModelUploadCheckInInputDto
{
    /// <summary>
    /// 要關聯到每筆 HIST 紀錄的操作人員帳號清單。
    /// </summary>
    public List<string>? Account { get; set; }

    /// <summary>
    /// 要驗證並依既有 WIP 機台關聯流程寫入的機台編號清單。
    /// </summary>
    public List<string>? Equipment { get; set; }

    /// <summary>
    /// 上模開始時間。
    /// </summary>
    public DateTime CheckInTime { get; set; }

    /// <summary>
    /// 產生 HIST 紀錄時要驗證的工序編號。
    /// </summary>
    public string Operation { get; set; } = null!;

    /// <summary>
    /// 產生 HIST 紀錄時要驗證的部門編號。
    /// </summary>
    public string Department { get; set; } = null!;

    /// <summary>
    /// 選填備註，會寫入產生的 HIST 紀錄。
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// 上模工單明細；同一個請求內所有明細必須使用相同 TolNo。
    /// </summary>
    public List<WipModelUploadCheckInDetailInputDto>? Details { get; set; } = new();
}

/// <summary>
/// 上模開始的一筆工單明細。
/// </summary>
public class WipModelUploadCheckInDetailInputDto
{
    /// <summary>
    /// 工單編號。
    /// </summary>
    public string WorkOrder { get; set; } = null!;

    /// <summary>
    /// TOL_MASTER 的模具編號。
    /// </summary>
    public string TolNo { get; set; } = null!;

    /// <summary>
    /// TOL_MASTER_DETAILS 的模具明細編號。
    /// </summary>
    public string TolDetalsNo { get; set; } = null!;

    /// <summary>
    /// WIP_PARTNO 的料號。
    /// </summary>
    public string PartNo { get; set; } = null!;

    /// <summary>
    /// 要寫入 WIP_OPI_WDOEACICO_HIST_CAV.OPI_CAV 的 CAV 數值。
    /// </summary>
    public decimal Cav { get; set; }
}

/// <summary>
/// 上模開始建立完成後的回傳資料。
/// </summary>
public class WipModelUploadCheckInResponseDto
{
    /// <summary>
    /// 新建立的 TOL history SID。
    /// </summary>
    [JsonPropertyName("tolSid")]
    public decimal TolSid { get; set; }

    /// <summary>
    /// 新建立的 HIST SID 清單。
    /// </summary>
    [JsonPropertyName("histSids")]
    public List<decimal> HistSids { get; set; } = new();
}
