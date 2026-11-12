using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Features.Labels;
using Fundoo.Notes.Domain.Entities;
using Npgsql;
using NpgsqlTypes;

namespace Fundoo.Notes.Infrastructure.Persistence;

public sealed class LabelRepository : ILabelRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public LabelRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<Guid>> GetNoteIdsForLabelAsync(
        Guid userId,
        Guid labelId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT nl.note_id
            FROM note_labels nl
            INNER JOIN labels l ON l.id = nl.label_id
            WHERE l.user_id = @userId AND l.id = @labelId;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("labelId", NpgsqlDbType.Uuid, labelId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var noteIds = new List<Guid>();
        while (await reader.ReadAsync(cancellationToken))
        {
            noteIds.Add(reader.GetGuid(0));
        }

        return noteIds;
    }

    public async Task<Label?> CreateAsync(Label label, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO labels (id, user_id, name, normalized_name, created_at_utc)
            VALUES (@id, @userId, @name, @normalizedName, @createdAtUtc)
            ON CONFLICT (user_id, normalized_name) DO NOTHING
            RETURNING id, user_id, name, normalized_name, created_at_utc;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, label.Id);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, label.UserId);
        command.Parameters.AddWithValue("name", NpgsqlDbType.Text, label.Name);
        command.Parameters.AddWithValue("normalizedName", NpgsqlDbType.Text, label.NormalizedName);
        command.Parameters.AddWithValue("createdAtUtc", NpgsqlDbType.TimestampTz, label.CreatedAtUtc.UtcDateTime);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadLabel(reader);
    }

    public async Task<IReadOnlyList<Label>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, user_id, name, normalized_name, created_at_utc
            FROM labels
            WHERE user_id = @userId
            ORDER BY name;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var labels = new List<Label>();
        while (await reader.ReadAsync(cancellationToken))
        {
            labels.Add(ReadLabel(reader));
        }

        return labels;
    }

    public async Task<LabelRenameStatus> RenameAsync(
        Guid userId,
        Guid labelId,
        string name,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE labels
            SET name = @name, normalized_name = @normalizedName
            WHERE user_id = @userId AND id = @labelId
            RETURNING id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("labelId", NpgsqlDbType.Uuid, labelId);
        command.Parameters.AddWithValue("name", NpgsqlDbType.Text, name);
        command.Parameters.AddWithValue("normalizedName", NpgsqlDbType.Text, normalizedName);

        try
        {
            var updatedId = await command.ExecuteScalarAsync(cancellationToken);
            if (updatedId is null)
            {
                return LabelRenameStatus.NotFound;
            }

            return LabelRenameStatus.Renamed;
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == "ix_labels_user_id_normalized_name")
        {
            return LabelRenameStatus.DuplicateName;
        }
    }

    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid labelId,
        CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM labels WHERE user_id = @userId AND id = @labelId;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("labelId", NpgsqlDbType.Uuid, labelId);
        var deletedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return deletedRows > 0;
    }

    public async Task<bool> AttachToNoteAsync(
        Guid userId,
        Guid noteId,
        Guid labelId,
        CancellationToken cancellationToken)
    {
        var resourcesBelongToUser = await ResourcesBelongToUserAsync(
            userId,
            noteId,
            labelId,
            cancellationToken);
        if (!resourcesBelongToUser)
        {
            return false;
        }

        const string sql = """
            INSERT INTO note_labels (note_id, label_id)
            VALUES (@noteId, @labelId)
            ON CONFLICT (note_id, label_id) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
        command.Parameters.AddWithValue("labelId", NpgsqlDbType.Uuid, labelId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveFromNoteAsync(
        Guid userId,
        Guid noteId,
        Guid labelId,
        CancellationToken cancellationToken)
    {
        var resourcesBelongToUser = await ResourcesBelongToUserAsync(
            userId,
            noteId,
            labelId,
            cancellationToken);
        if (!resourcesBelongToUser)
        {
            return false;
        }

        const string sql = """
            DELETE FROM note_labels
            WHERE note_id = @noteId AND label_id = @labelId;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
        command.Parameters.AddWithValue("labelId", NpgsqlDbType.Uuid, labelId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private async Task<bool> ResourcesBelongToUserAsync(
        Guid userId,
        Guid noteId,
        Guid labelId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                EXISTS (
                    SELECT 1 FROM notes
                    WHERE id = @noteId AND user_id = @userId AND is_trashed = FALSE
                )
                AND EXISTS (
                    SELECT 1 FROM labels
                    WHERE id = @labelId AND user_id = @userId
                );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
        command.Parameters.AddWithValue("labelId", NpgsqlDbType.Uuid, labelId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private static Label ReadLabel(NpgsqlDataReader reader)
    {
        var createdAtUtc = DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc);
        return new Label
        {
            Id = reader.GetGuid(0),
            UserId = reader.GetGuid(1),
            Name = reader.GetString(2),
            NormalizedName = reader.GetString(3),
            CreatedAtUtc = new DateTimeOffset(createdAtUtc)
        };
    }
}
