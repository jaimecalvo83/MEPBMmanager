using System.Net;
using System.Net.Mail;

namespace MEPBMmanager.Api.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody);
}

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(SmtpSettings settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        if (string.IsNullOrEmpty(_settings.Host))
        {
            _logger.LogWarning("SMTP not configured – email to {To} skipped", to);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.From, _settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            Credentials = new NetworkCredential(_settings.Username, _settings.Password),
            EnableSsl = _settings.EnableSsl
        };

        await client.SendMailAsync(message);
        _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
    }
}

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string FromName { get; set; } = "MEPBM Manager";
}
