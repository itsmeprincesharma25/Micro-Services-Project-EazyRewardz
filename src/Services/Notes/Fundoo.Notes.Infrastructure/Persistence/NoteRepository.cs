using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Features.Notes;
using Fundoo.Notes.Domain.Entities;
using Npgsql;
using NpgsqlTypes;

namespace Fundoo.Notes.Infrastructure.Persistence;

public sealed class NoteRepository : INoteRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _timeProvider;

    public NoteRepository(NpgsqlDataSource dataSource, TimeProvider timeProvider)
    {
        _dataSource = dataSource;
        _timeProvider = timeProvider;
    }

    public async Task<Note> CreateAsync(Note note, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO notes (id, user_id, title, content, is_archived, is_trashed, created_at_utc, updated_at_utc)
            VALUES (@id, @userId, @title, @content, @isArchived, @isTrashed, @createdAtUtc, @updatedAtUtc)
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, note.Id);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, note.UserId);
        command.Parameters.AddWithValue("title", NpgsqlDbType.Text, note.Title);
        command.Parameters.AddWithValue("content", NpgsqlDbType.Text, note.Content);
        command.Parameters.AddWithValue("isArchived", NpgsqlDbType.Boolean, note.IsArchived);
        command.Parameters.AddWithValue("isTrashed", NpgsqlDbType.Boolean, note.IsTrashed);
        command.Parameters.AddWithValue("createdAtUtc", NpgsqlDbType.TimestampTz, note.CreatedAtUtc.UtcDateTime);
        command.Parameters.AddWithValue("updatedAtUtc", NpgsqlDbType.TimestampTz, note.UpdatedAtUtc.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return note;
    }

    public async Task<IReadOnlyList<Note>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.id, n.user_id, n.title, n.content, n.is_archived, n.is_trashed,
                   n.created_at_utc, n.updated_at_utc, l.id, l.name, l.created_at_utc
            FROM notes n
            LEFT JOIN note_labels nl ON nl.note_id = n.id
            LEFT JOIN labels l ON l.id = nl.label_id AND l.user_id = n.user_id
            WHERE (n.user_id = @userId OR EXISTS (
                       SELECT 1 FROM note_collaborators nc
                       WHERE nc.note_id = n.id AND nc.collaborator_user_id = @userId
                   ))
              AND n.is_archived = FALSE AND n.is_trashed = FALSE
            ORDER BY n.updated_at_utc DESC, l.name;
            """;

        return await QueryNotesAsync(sql, userId, null, cancellationToken);
    }

    public async Task<IReadOnlyList<Note>> GetArchivedForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.id, n.user_id, n.title, n.content, n.is_archived, n.is_trashed,
                   n.created_at_utc, n.updated_at_utc, l.id, l.name, l.created_at_utc
            FROM notes n
            LEFT JOIN note_labels nl ON nl.note_id = n.id
            LEFT JOIN labels l ON l.id = nl.label_id AND l.user_id = n.user_id
            WHERE (n.user_id = @userId OR EXISTS (
                       SELECT 1 FROM note_collaborators nc
                       WHERE nc.note_id = n.id AND nc.collaborator_user_id = @userId
                   ))
              AND n.is_archived = TRUE AND n.is_trashed = FALSE
            ORDER BY n.updated_at_utc DESC, l.name;
            """;

        return await QueryNotesAsync(sql, userId, null, cancellationToken);
    }

    public async Task<IReadOnlyList<Note>> GetTrashedForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.id, n.user_id, n.title, n.content, n.is_archived, n.is_trashed,
                   n.created_at_utc, n.updated_at_utc, l.id, l.name, l.created_at_utc
            FROM notes n
            LEFT JOIN note_labels nl ON nl.note_id = n.id
            LEFT JOIN labels l ON l.id = nl.label_id AND l.user_id = n.user_id
            WHERE n.user_id = @userId AND n.is_trashed = TRUE
            ORDER BY n.updated_at_utc DESC, l.name;
            """;

        return await QueryNotesAsync(sql, userId, null, cancellationToken);
    }

    public async Task<Note?> GetByIdAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.id, n.user_id, n.title, n.content, n.is_archived, n.is_trashed,
                   n.created_at_utc, n.updated_at_utc, l.id, l.name, l.created_at_utc
            FROM notes n
            LEFT JOIN note_labels nl ON nl.note_id = n.id
            LEFT JOIN labels l ON l.id = nl.label_id AND l.user_id = n.user_id
            WHERE n.id = @noteId AND n.is_trashed = FALSE
              AND (n.user_id = @userId OR EXISTS (
                       SELECT 1 FROM note_collaborators nc
                       WHERE nc.note_id = n.id AND nc.collaborator_user_id = @userId
                   ))
            ORDER BY l.name;
            """;
        var notes = await QueryNotesAsync(sql, userId, noteId, cancellationToken);
        return notes.FirstOrDefault();
    }

    public async Task<bool> IsOwnedByUserAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM notes
                WHERE id = @noteId AND user_id = @userId AND is_trashed = FALSE
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    public async Task<CollaboratorAddStatus> AddCollaboratorAsync(
        Guid ownerUserId,
        Guid noteId,
        Guid collaboratorUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO note_collaborators (note_id, collaborator_user_id, created_at_utc)
            SELECT @noteId, @collaboratorUserId, @createdAtUtc
            WHERE EXISTS (
                SELECT 1 FROM notes
                WHERE id = @noteId AND user_id = @ownerUserId AND is_trashed = FALSE
            )
            ON CONFLICT (note_id, collaborator_user_id) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ownerUserId", NpgsqlDbType.Uuid, ownerUserId);
        command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
        command.Parameters.AddWithValue("collaboratorUserId", NpgsqlDbType.Uuid, collaboratorUserId);
        command.Parameters.AddWithValue(
            "createdAtUtc",
            NpgsqlDbType.TimestampTz,
            _timeProvider.GetUtcNow().UtcDateTime);
        var insertedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (insertedRows > 0)
        {
            return CollaboratorAddStatus.Added;
        }

        var noteIsOwned = await IsOwnedByUserAsync(ownerUserId, noteId, cancellationToken);
        if (!noteIsOwned)
        {
            return CollaboratorAddStatus.NoteNotFound;
        }

        return CollaboratorAddStatus.AlreadyCollaborator;
    }

    public async Task<IReadOnlyList<Guid>?> GetCollaboratorsAsync(
        Guid ownerUserId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var noteIsOwned = await IsOwnedByUserAsync(ownerUserId, noteId, cancellationToken);
        if (!noteIsOwned)
        {
            return null;
        }

        const string sql = """
            SELECT collaborator_user_id
            FROM note_collaborators
            WHERE note_id = @noteId
            ORDER BY created_at_utc;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var collaboratorIds = new List<Guid>();
        while (await reader.ReadAsync(cancellationToken))
        {
            collaboratorIds.Add(reader.GetGuid(0));
        }

        return collaboratorIds;
    }

    public async Task<bool> RemoveCollaboratorAsync(
        Guid ownerUserId,
        Guid noteId,
        Guid collaboratorUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM note_collaborators nc
            USING notes n
            WHERE nc.note_id = n.id
              AND n.id = @noteId
              AND n.user_id = @ownerUserId
              AND nc.collaborator_user_id = @collaboratorUserId;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ownerUserId", NpgsqlDbType.Uuid, ownerUserId);
        command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
        command.Parameters.AddWithValue("collaboratorUserId", NpgsqlDbType.Uuid, collaboratorUserId);
        var removedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return removedRows > 0;
    }

    public async Task<Note?> UpdateAsync(
        Guid userId,
        Guid noteId,
        string title,
        string content,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE notes
            SET title = @title, content = @content, updated_at_utc = @updatedAtUtc
            WHERE id = @noteId AND is_trashed = FALSE
              AND (user_id = @userId OR EXISTS (
                       SELECT 1 FROM note_collaborators nc
                       WHERE nc.note_id = notes.id AND nc.collaborator_user_id = @userId
                   ));
            """;

        int rowsUpdated;
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken))
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
            command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
            command.Parameters.AddWithValue("title", NpgsqlDbType.Text, title);
            command.Parameters.AddWithValue("content", NpgsqlDbType.Text, content);
            command.Parameters.AddWithValue("updatedAtUtc", NpgsqlDbType.TimestampTz, updatedAtUtc.UtcDateTime);
            rowsUpdated = await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (rowsUpdated == 0)
        {
            return null;
        }

        return await GetByIdAsync(userId, noteId, cancellationToken);
    }

    public Task<bool> SetArchivedAsync(
        Guid userId,
        Guid noteId,
        bool isArchived,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE notes
            SET is_archived = @isArchived, updated_at_utc = @updatedAtUtc
            WHERE user_id = @userId AND id = @noteId AND is_trashed = FALSE;
            """;

        return UpdateArchiveStatusAsync(sql, userId, noteId, isArchived, updatedAtUtc, cancellationToken);
    }

    public Task<bool> TrashAsync(
        Guid userId,
        Guid noteId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE notes
            SET is_trashed = TRUE, updated_at_utc = @updatedAtUtc
            WHERE user_id = @userId AND id = @noteId AND is_trashed = FALSE;
            """;

        return UpdateStateAsync(sql, userId, noteId, updatedAtUtc, cancellationToken);
    }

    public Task<bool> RestoreAsync(
        Guid userId,
        Guid noteId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE notes
            SET is_trashed = FALSE, updated_at_utc = @updatedAtUtc
            WHERE user_id = @userId AND id = @noteId AND is_trashed = TRUE;
            """;

        return UpdateStateAsync(sql, userId, noteId, updatedAtUtc, cancellationToken);
    }

    public async Task<bool> PermanentlyDeleteAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM notes
            WHERE user_id = @userId AND id = @noteId AND is_trashed = TRUE;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
        var deletedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return deletedRows > 0;
    }

    private async Task<bool> UpdateArchiveStatusAsync(
        string sql,
        Guid userId,
        Guid noteId,
        bool isArchived,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
        command.Parameters.AddWithValue("updatedAtUtc", NpgsqlDbType.TimestampTz, updatedAtUtc.UtcDateTime);
        command.Parameters.AddWithValue("isArchived", NpgsqlDbType.Boolean, isArchived);
        var changedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return changedRows > 0;
    }

    private async Task<bool> UpdateStateAsync(
        string sql,
        Guid userId,
        Guid noteId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId);
        command.Parameters.AddWithValue("updatedAtUtc", NpgsqlDbType.TimestampTz, updatedAtUtc.UtcDateTime);
        var changedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return changedRows > 0;
    }

    private async Task<IReadOnlyList<Note>> QueryNotesAsync(
        string sql,
        Guid userId,
        Guid? noteId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        if (noteId.HasValue)
        {
            command.Parameters.AddWithValue("noteId", NpgsqlDbType.Uuid, noteId.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var notesById = new Dictionary<Guid, Note>();

        // A note can have several labels, so the SQL join returns one row per label.
        // Group those rows here so the result contains one Note with its Labels list.
        while (await reader.ReadAsync(cancellationToken))
        {
            var noteIdFromRow = reader.GetGuid(0);
            if (!notesById.TryGetValue(noteIdFromRow, out var note))
            {
                note = ReadNote(reader);
                notesById.Add(note.Id, note);
            }

            if (!reader.IsDBNull(8))
            {
                note.Labels.Add(ReadLabelForNote(reader, note.UserId));
            }
        }

        return notesById.Values.ToArray();
    }

    private static Note ReadNote(NpgsqlDataReader reader)
    {
        var createdAtUtc = DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc);
        var updatedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc);

        return new Note
        {
            Id = reader.GetGuid(0),
            UserId = reader.GetGuid(1),
            Title = reader.GetString(2),
            Content = reader.GetString(3),
            IsArchived = reader.GetBoolean(4),
            IsTrashed = reader.GetBoolean(5),
            CreatedAtUtc = new DateTimeOffset(createdAtUtc),
            UpdatedAtUtc = new DateTimeOffset(updatedAtUtc)
        };
    }

    private static Label ReadLabelForNote(NpgsqlDataReader reader, Guid userId)
    {
        var createdAtUtc = DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc);
        var name = reader.GetString(9);
        return new Label
        {
            Id = reader.GetGuid(8),
            UserId = userId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            CreatedAtUtc = new DateTimeOffset(createdAtUtc)
        };
    }
}
