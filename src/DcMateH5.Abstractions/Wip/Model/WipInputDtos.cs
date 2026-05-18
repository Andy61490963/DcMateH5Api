namespace DcMateH5Api.Areas.Wip.Model;

public class WipAddDetailInputDto
{
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public decimal OK_QTY { get; set; }
    public decimal NG_QTY { get; set; }
    public string? COMMENT { get; set; }
    public List<NgDetailItem>? NgDetails { get; set; } = new();
}

public class NgDetailItem
{
    public decimal NG_QTY { get; set; }
    public string NG_CODE { get; set; }
    public string Comment { get; set; }
}

public class WipCheckOutInputDto
{
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public DateTime CHECK_OUT_TIME { get; set; }
}

public class WipEditDetailInputDto
{
    public decimal WIP_OPI_WDOEACICO_HIST_DETAIL_SID { get; set; }
    public decimal WIP_OPI_WDOEACICO_HIST_SID { get; set; }
    public decimal OK_QTY { get; set; }
    public decimal NG_QTY { get; set; }
    public string? COMMENT { get; set; }
    public List<NgDetailItem>? NgDetails { get; set; } = new();
}

public class WipDeleteDetailInputDto
{
    public decimal WIP_OPI_WDOEACICO_HIST_DETAIL_SID { get; set; }
}

/// <summary>
/// 下模結束資料。
/// </summary>
public class WipModelUploadCheckOutInputDto
{
    /// <summary>
    /// TOL history SID。
    /// </summary>
    public decimal WIP_OPI_WDOEACICO_HIST_TOL_SID { get; set; }

    /// <summary>
    /// 下模結束時間，會寫入 MODLE_REMOVE_END 與相關出站結束時間。
    /// </summary>
    public DateTime CHECK_OUT_TIME { get; set; }
}

/// <summary>
/// 編輯上模 CAV 數值的資料。
/// </summary>
public class WipEditModelUploadCavInputDto
{
    /// <summary>
    /// CAV history SID。
    /// </summary>
    public decimal WIP_OPI_WDOEACICO_HIST_CAV_SID { get; set; }

    /// <summary>
    /// 新的 CAV 數值。
    /// </summary>
    public decimal OPI_CAV { get; set; }
}

/// <summary>
/// 編輯上模結束時間的資料。
/// </summary>
public class WipEditModelUploadEndInputDto
{
    /// <summary>
    /// TOL history SID。
    /// </summary>
    public decimal WIP_OPI_WDOEACICO_HIST_TOL_SID { get; set; }

    /// <summary>
    /// 上模結束時間。
    /// </summary>
    public DateTime MODLE_UPLOAD_END { get; set; }
}

/// <summary>
/// 編輯下模開始時間的資料。
/// </summary>
public class WipEditModelRemoveStartInputDto
{
    /// <summary>
    /// TOL history SID。
    /// </summary>
    public decimal WIP_OPI_WDOEACICO_HIST_TOL_SID { get; set; }

    /// <summary>
    /// 下模開始時間。
    /// </summary>
    public DateTime MODLE_REMOVE_START { get; set; }
}
