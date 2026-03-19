namespace DcMateH5.Abstractions.Token.Model;

/// <summary>
/// Token 產生結果
/// </summary>
public sealed class GenerateTokenResult
{
    /// <summary>
    /// Token 字串
    /// </summary>
    public string TokenKey { get; init; } = string.Empty;

    /// <summary>
    /// Token 過期時間（）
    /// </summary>
    public DateTime Expiration { get; init; }
}

/// <summary>
/// Token 續期結果
/// </summary>
public sealed class RenewTokenResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 訊息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 帳號
    /// </summary>
    public string AccountNo { get; init; } = string.Empty;

    /// <summary>
    /// 新 Token
    /// </summary>
    public string TokenKey { get; init; } = string.Empty;

    /// <summary>
    /// 過期時間
    /// </summary>
    public DateTime? ExpirationText { get; init; }

    /// <summary>
    /// Token 序號
    /// </summary>
    public int TokenSeq { get; init; }
}

/// <summary>
/// Token 驗證結果
/// </summary>
public sealed class TokenValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 驗證訊息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 使用者 Id
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// 帳號
    /// </summary>
    public string Account { get; init; } = string.Empty;

    /// <summary>
    /// Session Id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Token 序號
    /// </summary>
    public int TokenSeq { get; init; }

    /// <summary>
    /// Token 有效分鐘數
    /// </summary>
    public int TokenMinutes { get; init; }

    /// <summary>
    /// 網域
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// Token 過期時間（ ticks）
    /// </summary>
    public long ExpireTicks { get; init; }

    /// <summary>
    /// Token 簽發時間（ ticks）
    /// </summary>
    public long IssuedTicks { get; init; }
    
    public string UserLv { get; init; } = string.Empty;
}