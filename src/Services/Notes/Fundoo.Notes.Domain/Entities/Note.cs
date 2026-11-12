namespace Fundoo.Notes.Domain.Entities;

public sealed class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public bool IsTrashed { get; set; }
    public List<Label> Labels { get; set; } = new List<Label>();
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
