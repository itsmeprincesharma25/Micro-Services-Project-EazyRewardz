using FluentValidation;
using Fundoo.Notes.Api.Contracts;

namespace Fundoo.Notes.Api.Validation;

public sealed class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Content).MaximumLength(20000);
    }
}

public sealed class UpdateNoteRequestValidator : AbstractValidator<UpdateNoteRequest>
{
    public UpdateNoteRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Content).MaximumLength(20000);
    }
}

public sealed class CreateLabelRequestValidator : AbstractValidator<CreateLabelRequest>
{
    public CreateLabelRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class RenameLabelRequestValidator : AbstractValidator<RenameLabelRequest>
{
    public RenameLabelRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class AddCollaboratorRequestValidator : AbstractValidator<AddCollaboratorRequest>
{
    public AddCollaboratorRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
    }
}
