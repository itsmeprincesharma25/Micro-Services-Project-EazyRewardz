using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Api.Contracts;

public sealed record NoteResponse(
    Guid Id,
    string Title,
    string Content,
    bool IsArchived,
    bool IsTrashed,
    IReadOnlyList<LabelResponse> Labels,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static NoteResponse From(Note note)
    {
        return new NoteResponse(
            note.Id,
            note.Title,
            note.Content,
            note.IsArchived,
            note.IsTrashed,
            note.Labels.Select(LabelResponse.From).ToArray(),
            note.CreatedAtUtc,
            note.UpdatedAtUtc);
    }
}
