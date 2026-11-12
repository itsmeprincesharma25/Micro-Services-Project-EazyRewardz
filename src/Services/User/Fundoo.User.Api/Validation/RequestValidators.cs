using FluentValidation;
using Fundoo.User.Api.Contracts.Authentication;
using Fundoo.User.Api.Contracts.Internal;

namespace Fundoo.User.Api.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().Length(12, 128);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Password).NotEmpty();
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Token).NotEmpty();
        RuleFor(request => request.NewPassword).NotEmpty().Length(12, 128);
    }
}

public sealed class UserLookupRequestValidator : AbstractValidator<UserLookupRequest>
{
    public UserLookupRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
    }
}
