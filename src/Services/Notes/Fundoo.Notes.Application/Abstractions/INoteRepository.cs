using Fundoo.Notes.Application.Features.Notes;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Abstractions;

public interface INoteRepository
{
    Task<Note> CreateAsync(Note note, CancellationToken cancellationToken);
    Task<IReadOnlyList<Note>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Note>> GetArchivedForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Note>> GetTrashedForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<Note?> GetByIdAsync(Guid userId, Guid noteId, CancellationToken cancellationToken);
    Task<bool> IsOwnedByUserAsync(Guid userId, Guid noteId, CancellationToken cancellationToken);
    Task<CollaboratorAddStatus> AddCollaboratorAsync(
        Guid ownerUserId,
        Guid noteId,
        Guid collaboratorUserId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>?> GetCollaboratorsAsync(
        Guid ownerUserId,
        Guid noteId,
        CancellationToken cancellationToken);
    Task<bool> RemoveCollaboratorAsync(
        Guid ownerUserId,
        Guid noteId,
        Guid collaboratorUserId,
        CancellationToken cancellationToken);
    Task<Note?> UpdateAsync(
        Guid userId,
        Guid noteId,
        string title,
        string content,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
    Task<bool> SetArchivedAsync(
        Guid userId,
        Guid noteId,
        bool isArchived,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
    Task<bool> TrashAsync(
        Guid userId,
        Guid noteId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
    Task<bool> RestoreAsync(
        Guid userId,
        Guid noteId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
    Task<bool> PermanentlyDeleteAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken);
}
