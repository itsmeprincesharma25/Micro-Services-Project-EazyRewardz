using Fundoo.Notification.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.IO;
using System.Net.Sockets;

namespace Fundoo.Notification.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<SmtpOptions> _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options;
    }

    public async Task SendAsync(
        string recipient,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        var smtp = _options.Value;
        ValidateOptions(smtp);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            TextBody = textBody,
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var client = new SmtpClient
        {
            Timeout = 30_000
        };

        try
        {
            await client.ConnectAsync(
                smtp.Host,
                smtp.Port,
                GetSecurityOption(smtp.Security),
                cancellationToken);
            await client.AuthenticateAsync(smtp.UserName, smtp.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsDeliveryFailure(exception))
        {
            throw new EmailDeliveryUnavailableException(
                "The SMTP server could not complete email delivery.",
                exception);
        }
    }

    private static void ValidateOptions(SmtpOptions smtp)
    {
        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            throw new InvalidOperationException("SMTP host is not configured.");
        }

        if (smtp.Port < 1)
        {
            throw new InvalidOperationException("SMTP port must be between 1 and 65535.");
        }

        if (smtp.Port > 65535)
        {
            throw new InvalidOperationException("SMTP port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(smtp.UserName))
        {
            throw new InvalidOperationException("SMTP username is not configured.");
        }

        if (string.IsNullOrWhiteSpace(smtp.Password))
        {
            throw new InvalidOperationException("SMTP password is not configured.");
        }

        if (string.IsNullOrWhiteSpace(smtp.FromAddress))
        {
            throw new InvalidOperationException("SMTP sender address is not configured.");
        }
    }

    private static SecureSocketOptions GetSecurityOption(string security)
    {
        if (string.Equals(security, "StartTls", StringComparison.OrdinalIgnoreCase))
        {
            return SecureSocketOptions.StartTls;
        }

        if (string.Equals(security, "SslOnConnect", StringComparison.OrdinalIgnoreCase))
        {
            return SecureSocketOptions.SslOnConnect;
        }

        throw new InvalidOperationException(
            "SMTP security must be StartTls or SslOnConnect.");
    }

    private static bool IsDeliveryFailure(Exception exception)
    {
        if (exception is SmtpCommandException)
        {
            return true;
        }

        if (exception is SmtpProtocolException)
        {
            return true;
        }

        if (exception is SocketException)
        {
            return true;
        }

        if (exception is IOException)
        {
            return true;
        }

        if (exception is TimeoutException)
        {
            return true;
        }

        return false;
    }
}
