using System.IdentityModel.Tokens.Jwt;
using Fundoo.Notes.Api.Contracts;
using Fundoo.Notes.Application.Features.Labels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/labels")]
public sealed class LabelsController : ControllerBase
{
    private readonly LabelService _labelService;

    public LabelsController(LabelService labelService)
    {
        _labelService = labelService;
    }

    [HttpPost]
    [ProducesResponseType<LabelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLabelRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var label = await _labelService.CreateAsync(userId.Value, request.Name, cancellationToken);
        if (label is null)
        {
            return Conflict(new { message = "A label with this name already exists." });
        }

        var response = LabelResponse.From(label);
        return CreatedAtAction(nameof(GetAll), response);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LabelResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var labels = await _labelService.GetForUserAsync(userId.Value, cancellationToken);
        return Ok(labels.Select(LabelResponse.From).ToArray());
    }

    [HttpPut("{labelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Rename(
        Guid labelId,
        [FromBody] RenameLabelRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _labelService.RenameAsync(
            userId.Value,
            labelId,
            request.Name,
            cancellationToken);

        if (result == LabelRenameStatus.NotFound)
        {
            return NotFound();
        }

        if (result == LabelRenameStatus.DuplicateName)
        {
            return Conflict(new { message = "A label with this name already exists." });
        }

        return NoContent();
    }

    [HttpDelete("{labelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid labelId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var wasDeleted = await _labelService.DeleteAsync(userId.Value, labelId, cancellationToken);
        if (!wasDeleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
}
