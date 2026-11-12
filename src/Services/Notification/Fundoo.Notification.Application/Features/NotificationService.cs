using System.Text.Encodings.Web;
using System.Security.Cryptography;
using System.Text;
using Fundoo.Notification.Application.Abstractions;

namespace Fundoo.Notification.Application.Features;

public sealed class NotificationService
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailRequestStore _emailRequestStore;

    public NotificationService(IEmailSender emailSender, IEmailRequestStore emailRequestStore)
    {
        _emailSender = emailSender;
        _emailRequestStore = emailRequestStore;
    }

    public async Task<EmailRequestClaimResult> SendPasswordResetEmailAsync(
        PasswordResetEmailRequest request,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var requestHash = CreateRequestHash(request);
        var claimResult = await _emailRequestStore.TryClaimAsync(
            idempotencyKey,
            requestHash,
            cancellationToken);

        if (claimResult != EmailRequestClaimResult.Claimed)
        {
            return claimResult;
        }

        var safeEmail = HtmlEncoder.Default.Encode(request.Email);
        var safeToken = HtmlEncoder.Default.Encode(request.ResetToken);
        var subject = "Reset your Fundoo password";
        var textBody =
            $"We received a request to reset the Fundoo password for {request.Email}. " +
            $"Use this reset token within 15 minutes: {request.ResetToken}";
        var htmlBody = $"""
            <p>We received a request to reset the Fundoo password for {safeEmail}.</p>
            <p>Use this reset token within 15 minutes:</p>
            <p><code>{safeToken}</code></p>
            <p>If you did not request this, you can ignore this email.</p>
            """;

        try
        {
            await _emailSender.SendAsync(
                request.Email,
                subject,
                textBody,
                htmlBody,
                cancellationToken);

            await _emailRequestStore.MarkCompletedAsync(idempotencyKey, cancellationToken);
            return EmailRequestClaimResult.Claimed;
        }
        catch
        {
            // Allow a later retry to reclaim this key, then preserve the original failure.
            if (!cancellationToken.IsCancellationRequested)
            {
                await _emailRequestStore.MarkFailedAsync(idempotencyKey, CancellationToken.None);
            }

            throw;
        }
    }

    private static string CreateRequestHash(PasswordResetEmailRequest request)
    {
        var value = $"{request.Email.Trim().ToUpperInvariant()}\n{request.ResetToken}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
