namespace DcMateH5Api.Areas.Wip.Model;

public class WipCheckInInputDto
{
    /// <summary>
    /// UMM_USER 的 ACCOUNT_NO (複數個，可為 NULL)
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
