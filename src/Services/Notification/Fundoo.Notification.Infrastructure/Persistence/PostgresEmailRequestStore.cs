using Fundoo.Notification.Application.Abstractions;
using Npgsql;

namespace Fundoo.Notification.Infrastructure.Persistence;

public sealed class PostgresEmailRequestStore : IEmailRequestStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresEmailRequestStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<EmailRequestClaimResult> TryClaimAsync(
        Guid idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO notification_email_requests (idempotency_key, request_hash, status)
            VALUES (@key, @hash, 'processing')
            ON CONFLICT (idempotency_key) DO NOTHING
            RETURNING idempotency_key;
            """,
            connection);
        insert.Parameters.AddWithValue("key", idempotencyKey);
        insert.Parameters.AddWithValue("hash", requestHash);

        var insertedKey = await insert.ExecuteScalarAsync(cancellationToken);
        if (insertedKey is not null)
        {
            return EmailRequestClaimResult.Claimed;
        }

        await using var existing = new NpgsqlCommand(
            """
            SELECT request_hash, status
            FROM notification_email_requests
            WHERE idempotency_key = @key;
            """,
            connection);
        existing.Parameters.AddWithValue("key", idempotencyKey);

        await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The email request record could not be read.");
        }

        var storedHash = reader.GetString(0);
        var status = reader.GetString(1);
        await reader.CloseAsync();

        if (!string.Equals(storedHash, requestHash, StringComparison.Ordinal))
        {
            return EmailRequestClaimResult.KeyConflict;
        }

        if (string.Equals(status, "completed", StringComparison.Ordinal))
        {
            return EmailRequestClaimResult.AlreadyCompleted;
        }

        if (string.Equals(status, "processing", StringComparison.Ordinal))
        {
            return EmailRequestClaimResult.AlreadyProcessing;
        }

        await using var reclaim = new NpgsqlCommand(
            """
            UPDATE notification_email_requests
            SET status = 'processing', updated_at_utc = now()
            WHERE idempotency_key = @key AND status = 'failed';
            """,
            connection);
        reclaim.Parameters.AddWithValue("key", idempotencyKey);
        var updatedRows = await reclaim.ExecuteNonQueryAsync(cancellationToken);

        return updatedRows == 1
            ? EmailRequestClaimResult.Claimed
            : EmailRequestClaimResult.AlreadyProcessing;
    }

    public Task MarkCompletedAsync(Guid idempotencyKey, CancellationToken cancellationToken)
    {
        return UpdateStatusAsync(idempotencyKey, "completed", cancellationToken);
    }

    public Task MarkFailedAsync(Guid idempotencyKey, CancellationToken cancellationToken)
    {
        return UpdateStatusAsync(idempotencyKey, "failed", cancellationToken);
    }

    private async Task UpdateStatusAsync(
        Guid idempotencyKey,
        string status,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            UPDATE notification_email_requests
            SET status = @status, updated_at_utc = now()
            WHERE idempotency_key = @key;
            """,
            connection);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("key", idempotencyKey);

        var updatedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (updatedRows != 1)
        {
            throw new InvalidOperationException("The email request status could not be updated.");
        }
    }
}
