namespace Fundoo.Notification.Application.Abstractions;

public enum EmailRequestClaimResult
{
    Claimed,
    AlreadyCompleted,
    AlreadyProcessing,
    KeyConflict
}

public interface IEmailRequestStore
{
    Task<EmailRequestClaimResult> TryClaimAsync(
        Guid idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken);

    Task MarkCompletedAsync(Guid idempotencyKey, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid idempotencyKey, CancellationToken cancellationToken);
}
