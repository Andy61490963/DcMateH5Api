namespace DcMateH5.Abstractions.Token.Model;

/// <summary>
/// Token 內容模型
/// </summary>
public sealed record TokenPayload
{
    /// <summary>
    /// Token 過期時間（ ticks）
    /// </summary>
    public long ExpireTicks { get; init; }

    /// <summary>
    /// 網域
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// Token 有效分鐘數
    /// </summary>
    public int TokenMinutes { get; init; }

    /// <summary>
    /// Token 序號
    /// </summary>
    public int TokenSeq { get; init; }

    /// <summary>
    /// 使用者 Id
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// 帳號
    /// </summary>
    public string Account { get; init; } = string.Empty;

    /// <summary>
    /// 這次登入會話 Id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Token 簽發時間（ ticks）
    /// </summary>
    public long IssuedTicks { get; init; }
    
    /// <summary>
    /// 等級
    /// </summary>
    public string? UserLv { get; init; }
}