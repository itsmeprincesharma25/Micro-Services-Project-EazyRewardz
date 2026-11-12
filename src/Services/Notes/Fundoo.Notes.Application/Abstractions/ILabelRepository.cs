using Fundoo.Notes.Application.Features.Labels;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Abstractions;

public interface ILabelRepository
{
    Task<Label?> CreateAsync(Label label, CancellationToken cancellationToken);
    Task<IReadOnlyList<Label>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetNoteIdsForLabelAsync(
        Guid userId,
        Guid labelId,
        CancellationToken cancellationToken);
    Task<LabelRenameStatus> RenameAsync(
        Guid userId,
        Guid labelId,
        string name,
        string normalizedName,
        CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid userId, Guid labelId, CancellationToken cancellationToken);
    Task<bool> AttachToNoteAsync(
        Guid userId,
        Guid noteId,
        Guid labelId,
        CancellationToken cancellationToken);
    Task<bool> RemoveFromNoteAsync(
        Guid userId,
        Guid noteId,
        Guid labelId,
        CancellationToken cancellationToken);
}
