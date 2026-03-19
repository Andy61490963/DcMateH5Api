namespace DcMateH5.Abstractions.Token.Model;

/// <summary>
/// Token 設定
/// </summary>
public sealed class TokenOptions
{
    /// <summary>
    /// 加密 Key（DES 需 8 碼）
    /// </summary>
    public string RgbKey { get; init; } = string.Empty;

    /// <summary>
    /// 加密 IV（DES 需 8 碼）
    /// </summary>
    public string RgbIV { get; init; } = string.Empty;

    /// <summary>
    /// Token 前綴字串
    /// </summary>
    public string PrefixWord { get; init; } = string.Empty;

    /// <summary>
    /// Domain
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// 預設 Token 分鐘數
    /// </summary>
    public int DefaultTokenKeyMinutes { get; init; }
}