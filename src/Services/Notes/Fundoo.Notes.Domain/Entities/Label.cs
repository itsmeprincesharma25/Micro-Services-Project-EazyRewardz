namespace Fundoo.Notes.Domain.Entities;

public sealed class Label
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
