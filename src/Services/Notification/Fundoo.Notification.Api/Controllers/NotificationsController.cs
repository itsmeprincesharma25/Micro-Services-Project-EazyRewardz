using Fundoo.Notification.Api.Contracts;
using Fundoo.Notification.Api.Services;
using Fundoo.Notification.Application.Features;
using Fundoo.Notification.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notification.Api.Controllers;

[ApiController]
[Route("api/internal/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly IInternalServiceKeyValidator _keyValidator;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        NotificationService notificationService,
        IInternalServiceKeyValidator keyValidator,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _keyValidator = keyValidator;
        _logger = logger;
    }

    [HttpPost("password-reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendPasswordResetEmail(
        [FromBody] Fundoo.Notification.Api.Contracts.PasswordResetEmailRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey == Guid.Empty)
        {
            return BadRequest(new { message = "A valid Idempotency-Key header is required." });
        }

        var serviceKey = Request.Headers["X-Fundoo-Service-Key"].ToString();
        if (!_keyValidator.IsValid(serviceKey))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _notificationService.SendPasswordResetEmailAsync(
                new Fundoo.Notification.Application.Features.PasswordResetEmailRequest(
                    request.Email,
                    request.ResetToken),
                idempotencyKey,
                cancellationToken);

            if (result == EmailRequestClaimResult.KeyConflict)
            {
                return Conflict(new { message = "The idempotency key was already used for a different email request." });
            }

            if (result == EmailRequestClaimResult.AlreadyProcessing)
            {
                return Accepted();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (EmailDeliveryUnavailableException exception)
        {
            _logger.LogWarning(
                exception,
                "Email delivery is unavailable for idempotency key {IdempotencyKey}.",
                idempotencyKey);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Email delivery is temporarily unavailable."
            });
        }

        return NoContent();
    }
}
