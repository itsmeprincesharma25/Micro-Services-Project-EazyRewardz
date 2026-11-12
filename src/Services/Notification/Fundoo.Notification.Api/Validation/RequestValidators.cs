using FluentValidation;
using Fundoo.Notification.Api.Contracts;

namespace Fundoo.Notification.Api.Validation;

public sealed class PasswordResetEmailRequestValidator : AbstractValidator<PasswordResetEmailRequest>
{
    public PasswordResetEmailRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.ResetToken).NotEmpty().Length(20, 256);
    }
}
