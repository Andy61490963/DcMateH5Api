namespace DcMateH5Api.Areas.Log.Models;

using System;

/// <summary>
/// Represents a single SQL execution log entry.
/// Maps to table columns: SEQNO, ID, USER_ID, EXECUTED_AT, DURATION_MS,
/// SQL_TEXT, PARAMETERS, AFFECTED_ROWS, IP_ADDRESS, ERROR_MESSAGE, IS_SUCCESS.
/// </summary>
public class SqlLogEntry
{
    /// <summary>Primary identifier (maps to ID).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Optional user id triggering the command.</summary>
    public Guid? UserId { get; set; }
    
    /// <summary>To group same Http request log.</summary>
    public Guid? RequestId { get; set; }

    /// <summary>Execution timestamp.</summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>Duration in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>The executed SQL text.</summary>
    public string SqlText { get; set; } = string.Empty;

    /// <summary>Serialized parameters.</summary>
    public string? Parameters { get; set; }

    /// <summary>Number of affected rows or returned records.</summary>
    public int AffectedRows { get; set; }

    /// <summary>Caller IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Error message when execution fails.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Indicates whether execution succeeded.</summary>
    public bool IsSuccess { get; set; }
}

