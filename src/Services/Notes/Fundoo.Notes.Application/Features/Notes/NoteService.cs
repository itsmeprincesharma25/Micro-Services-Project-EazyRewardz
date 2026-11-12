using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Features.Notes;

public sealed class NoteService
{
    private readonly INoteRepository _noteRepository;
    private readonly INoteCache _noteCache;
    private readonly IUserDirectoryClient _userDirectoryClient;
    private readonly TimeProvider _timeProvider;

    public NoteService(
        INoteRepository noteRepository,
        INoteCache noteCache,
        IUserDirectoryClient userDirectoryClient,
        TimeProvider timeProvider)
    {
        _noteRepository = noteRepository;
        _noteCache = noteCache;
        _userDirectoryClient = userDirectoryClient;
        _timeProvider = timeProvider;
    }

    public Task<Note> CreateAsync(
        Guid userId,
        string title,
        string content,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var note = new Note
        {
            UserId = userId,
            Title = title.Trim(),
            Content = content.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        return _noteRepository.CreateAsync(note, cancellationToken);
    }

    public Task<IReadOnlyList<Note>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _noteRepository.GetForUserAsync(userId, cancellationToken);
    }

    public Task<IReadOnlyList<Note>> GetArchivedForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _noteRepository.GetArchivedForUserAsync(userId, cancellationToken);
    }

    public Task<IReadOnlyList<Note>> GetTrashedForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _noteRepository.GetTrashedForUserAsync(userId, cancellationToken);
    }

    public async Task<Note?> GetByIdAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var cachedNote = await _noteCache.GetAsync(userId, noteId, cancellationToken);
        if (cachedNote is not null)
        {
            return cachedNote;
        }

        var note = await _noteRepository.GetByIdAsync(userId, noteId, cancellationToken);
        if (note is not null)
        {
            await _noteCache.SetAsync(note, cancellationToken);
        }

        return note;
    }

    public async Task<Note?> UpdateAsync(
        Guid userId,
        Guid noteId,
        string title,
        string content,
        CancellationToken cancellationToken)
    {
        var note = await _noteRepository.UpdateAsync(
            userId,
            noteId,
            title.Trim(),
            content.Trim(),
            _timeProvider.GetUtcNow(),
            cancellationToken);

        if (note is not null)
        {
            await _noteCache.RemoveAsync(note.UserId, noteId, cancellationToken);
        }

        return note;
    }

    public async Task<bool> SetArchivedAsync(
        Guid userId,
        Guid noteId,
        bool isArchived,
        CancellationToken cancellationToken)
    {
        var wasUpdated = await _noteRepository.SetArchivedAsync(
            userId,
            noteId,
            isArchived,
            _timeProvider.GetUtcNow(),
            cancellationToken);

        if (wasUpdated)
        {
            await _noteCache.RemoveAsync(userId, noteId, cancellationToken);
        }

        return wasUpdated;
    }

    public async Task<bool> TrashAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var wasTrashed = await _noteRepository.TrashAsync(
            userId,
            noteId,
            _timeProvider.GetUtcNow(),
            cancellationToken);

        if (wasTrashed)
        {
            await _noteCache.RemoveAsync(userId, noteId, cancellationToken);
        }

        return wasTrashed;
    }

    public async Task<bool> RestoreAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var wasRestored = await _noteRepository.RestoreAsync(
            userId,
            noteId,
            _timeProvider.GetUtcNow(),
            cancellationToken);

        if (wasRestored)
        {
            await _noteCache.RemoveAsync(userId, noteId, cancellationToken);
        }

        return wasRestored;
    }

    public async Task<bool> PermanentlyDeleteAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var wasDeleted = await _noteRepository.PermanentlyDeleteAsync(userId, noteId, cancellationToken);
        if (wasDeleted)
        {
            await _noteCache.RemoveAsync(userId, noteId, cancellationToken);
        }

        return wasDeleted;
    }

    public async Task<CollaboratorAddResult> AddCollaboratorAsync(
        Guid ownerUserId,
        Guid noteId,
        string collaboratorEmail,
        CancellationToken cancellationToken)
    {
        var noteIsOwned = await _noteRepository.IsOwnedByUserAsync(
            ownerUserId,
            noteId,
            cancellationToken);
        if (!noteIsOwned)
        {
            return new CollaboratorAddResult(CollaboratorAddStatus.NoteNotFound);
        }

        var collaboratorUserId = await _userDirectoryClient.FindUserIdByEmailAsync(
            collaboratorEmail,
            cancellationToken);
        if (collaboratorUserId is null)
        {
            return new CollaboratorAddResult(CollaboratorAddStatus.UserNotFound);
        }

        if (collaboratorUserId == ownerUserId)
        {
            return new CollaboratorAddResult(CollaboratorAddStatus.CannotAddSelf);
        }

        var status = await _noteRepository.AddCollaboratorAsync(
            ownerUserId,
            noteId,
            collaboratorUserId.Value,
            cancellationToken);

        return new CollaboratorAddResult(status, collaboratorUserId.Value);
    }

    public Task<IReadOnlyList<Guid>?> GetCollaboratorsAsync(
        Guid ownerUserId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        return _noteRepository.GetCollaboratorsAsync(ownerUserId, noteId, cancellationToken);
    }

    public Task<bool> RemoveCollaboratorAsync(
        Guid ownerUserId,
        Guid noteId,
        Guid collaboratorUserId,
        CancellationToken cancellationToken)
    {
        return _noteRepository.RemoveCollaboratorAsync(
            ownerUserId,
            noteId,
            collaboratorUserId,
            cancellationToken);
    }
}
