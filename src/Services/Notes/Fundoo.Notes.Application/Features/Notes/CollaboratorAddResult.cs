namespace Fundoo.Notes.Application.Features.Notes;

public sealed record CollaboratorAddResult(
    CollaboratorAddStatus Status,
    Guid? CollaboratorUserId = null);
