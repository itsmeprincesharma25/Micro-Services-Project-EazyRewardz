using Fundoo.User.Api.Contracts.Internal;
using Fundoo.User.Api.Services;
using Fundoo.User.Application.Features.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.User.Api.Controllers;

[ApiController]
[Route("internal/users")]
public sealed class InternalUsersController : ControllerBase
{
    private readonly UserAccountService _userAccountService;
    private readonly IInternalServiceKeyValidator _serviceKeyValidator;

    public InternalUsersController(
        UserAccountService userAccountService,
        IInternalServiceKeyValidator serviceKeyValidator)
    {
        _userAccountService = userAccountService;
        _serviceKeyValidator = serviceKeyValidator;
    }

    [HttpPost("lookup")]
    [ProducesResponseType<InternalUserLookupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindByEmail(
        [FromBody] UserLookupRequest request,
        [FromHeader(Name = "X-Fundoo-Service-Key")] string? serviceKey,
        CancellationToken cancellationToken)
    {
        if (!_serviceKeyValidator.IsValid(serviceKey))
        {
            return Unauthorized();
        }

        var userId = await _userAccountService.FindUserIdByEmailAsync(
            request.Email,
            cancellationToken);
        if (userId is null)
        {
            return NotFound();
        }

        return Ok(new InternalUserLookupResponse(userId.Value));
    }

    public sealed record InternalUserLookupResponse(Guid UserId);
}
