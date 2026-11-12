using Fundoo.User.Application.Features.Authentication;

namespace Fundoo.User.Application.Abstractions;

public interface IAccessTokenGenerator
{
    GeneratedAccessToken Create(Guid userId, string email);
}
