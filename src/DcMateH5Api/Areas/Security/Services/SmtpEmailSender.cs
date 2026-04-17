using System.Net;
using System.Net.Mail;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.Options;
using Microsoft.Extensions.Options;

namespace DcMateH5Api.Areas.Security.Services;

/// <summary>
/// SMTP email sender.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettingOptions _options;

    public SmtpEmailSender(IOptions<EmailSettingOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml,
        CancellationToken ct = default)
    {
        string host = string.IsNullOrWhiteSpace(_options.ExternalSMTP)
            ? _options.InternalSMTP
            : _options.ExternalSMTP;

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(_options.From)
            || string.IsNullOrWhiteSpace(_options.Sw))
        {
            throw new InvalidOperationException("EmailSetting is not configured.");
        }

        using SmtpClient client = new(host, _options.Port)
        {
            EnableSsl = _options.EnableSSL,
            Credentials = new NetworkCredential(_options.From, _options.Sw)
        };

        using MailMessage message = new()
        {
            From = new MailAddress(_options.From),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };
        message.To.Add(to);

        await client.SendMailAsync(message, ct);
    }
}
