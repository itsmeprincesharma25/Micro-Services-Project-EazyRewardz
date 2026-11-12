namespace Fundoo.Notes.Api.Contracts;

public sealed class UpdateNoteRequest
{
    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;
}
