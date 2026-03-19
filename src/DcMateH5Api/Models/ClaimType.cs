using System.Security.Claims;

namespace DcMateH5Api.Models;

public static class AppClaimTypes
{
    public const string UserId = "UserId";
    public const string UserLv = "UserLV";

    // 如果你未來要更靠近標準，也可以這樣包
    public static readonly string Account = ClaimTypes.Name;
}

/// <summary>
/// Token Claims 常數
/// </summary>
public static class TokenClaimTypes
{
    // 避免 magic string
    public const string SessionId = "session_id";
    public const string TokenSeq = "token_seq";
}