namespace DcMateH5Api.Areas.Security.Interfaces;

/// <summary>
/// Sends email messages.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml,
        CancellationToken ct = default);
}
