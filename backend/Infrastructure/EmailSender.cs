using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MinhaEscala.Infrastructure;

public sealed class EmailSender(IConfiguration config, IHostEnvironment environment, ILogger<EmailSender> logger)
{
    public async Task SendAsync(string email, string subject, string body, CancellationToken ct)
    {
        var host = config["SMTP_HOST"];
        var user = config["SMTP_USER"];
        if (string.IsNullOrWhiteSpace(host))
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Configure SMTP_HOST e SMTP_FROM em produção.");
            logger.LogWarning("EMAIL LOCAL (somente Development): {Subject}. {Body}", subject, body);
            return;
        }
        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config["SMTP_FROM"]!));
        message.To.Add(MailboxAddress.Parse(email)); message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        using var smtp = new SmtpClient();
        var security = config["SMTP_SECURITY"] == "SslOnConnect" ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await smtp.ConnectAsync(host, int.Parse(config["SMTP_PORT"] ?? "587"), security, ct);
        if (!string.IsNullOrWhiteSpace(user)) await smtp.AuthenticateAsync(user, config["SMTP_PASSWORD"] ?? "", ct);
        await smtp.SendAsync(message, ct); await smtp.DisconnectAsync(true, ct);
    }
}
