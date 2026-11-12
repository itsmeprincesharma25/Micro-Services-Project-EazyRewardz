using Fundoo.User.Api.Contracts.Authentication;
using Fundoo.User.Application.Features.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Fundoo.User.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController : ControllerBase
{
    private readonly UserAccountService _userAccountService;

    public AuthenticationController(UserAccountService userAccountService)
    {
        _userAccountService = userAccountService;
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userAccountService.RegisterAsync(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Conflict(new { message = result.ErrorMessage });
        }

        return StatusCode(StatusCodes.Status201Created, new { userId = result.UserId });
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userAccountService.LoginAsync(
            request.Email,
            request.Password,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(new
        {
            userId = result.UserId,
            email = result.Email,
            accessToken = result.AccessToken,
            tokenType = "Bearer",
            expiresAtUtc = result.AccessTokenExpiresAtUtc
        });
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);

        return Ok(new
        {
            userId,
            email
        });
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _userAccountService.RequestPasswordResetAsync(
            request.Email,
            cancellationToken);

        return Ok(new
        {
            message = "If an account exists for that email, a password reset email has been sent."
        });
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userAccountService.ResetPasswordAsync(
            request.Email,
            request.Token,
            request.NewPassword,
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(new { message = "Password has been reset successfully." });
    }
}
