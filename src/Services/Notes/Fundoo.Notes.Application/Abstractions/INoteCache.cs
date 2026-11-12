using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Abstractions;

public interface INoteCache
{
    Task<Note?> GetAsync(Guid userId, Guid noteId, CancellationToken cancellationToken);
    Task SetAsync(Note note, CancellationToken cancellationToken);
    Task RemoveAsync(Guid userId, Guid noteId, CancellationToken cancellationToken);
}
