using FitSocial.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace FitSocial.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var smtpHost = _configuration["MailSettings:Host"];
        var smtpPortStr = _configuration["MailSettings:Port"];
        var senderEmail = _configuration["MailSettings:SenderEmail"];
        var senderName = _configuration["MailSettings:SenderName"] ?? "FitSocial Support";
        var password = _configuration["MailSettings:Password"];

        try
        {
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail ?? "support@fitsocial.vn"));
            emailMessage.To.Add(new MailboxAddress(toEmail, toEmail));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            var port = int.TryParse(smtpPortStr, out var p) ? p : 587;

            await client.ConnectAsync(smtpHost, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(senderEmail ?? string.Empty, password);
            await client.SendAsync(emailMessage);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Đã gửi email thành công tới {ToEmail}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email tới {ToEmail}. Fallback sang Console logger.", toEmail);
            _logger.LogInformation("[DEV EMAIL FALLBACK] Tiêu đề: {Subject}, Gửi tới: {ToEmail}", subject, toEmail);
        }
    }
}
