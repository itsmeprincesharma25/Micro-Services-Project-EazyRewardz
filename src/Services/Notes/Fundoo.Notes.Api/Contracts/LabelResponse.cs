using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Api.Contracts;

public sealed record LabelResponse(Guid Id, string Name, DateTimeOffset CreatedAtUtc)
{
    public static LabelResponse From(Label label)
    {
        return new LabelResponse(label.Id, label.Name, label.CreatedAtUtc);
    }
}
