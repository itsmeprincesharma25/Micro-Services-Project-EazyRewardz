using System.IdentityModel.Tokens.Jwt;
using Fundoo.Notes.Api.Contracts;
using Fundoo.Notes.Application.Features.Labels;
using Fundoo.Notes.Application.Features.Notes;
using Fundoo.Notes.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notes")]
public sealed class NotesController : ControllerBase
{
    private readonly NoteService _noteService;
    private readonly LabelService _labelService;
    private readonly ILogger<NotesController> _logger;

    public NotesController(
        NoteService noteService,
        LabelService labelService,
        ILogger<NotesController> logger)
    {
        _noteService = noteService;
        _labelService = labelService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType<NoteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateNoteRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var note = await _noteService.CreateAsync(
            userId.Value,
            request.Title,
            request.Content,
            cancellationToken);
        var response = NoteResponse.From(note);
        return CreatedAtAction(nameof(GetById), new { id = note.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NoteResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var notes = await _noteService.GetForUserAsync(userId.Value, cancellationToken);
        return Ok(notes.Select(NoteResponse.From).ToArray());
    }

    [HttpGet("archived")]
    [ProducesResponseType<IReadOnlyList<NoteResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetArchived(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var notes = await _noteService.GetArchivedForUserAsync(userId.Value, cancellationToken);
        return Ok(notes.Select(NoteResponse.From).ToArray());
    }

    [HttpGet("trash")]
    [ProducesResponseType<IReadOnlyList<NoteResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTrash(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var notes = await _noteService.GetTrashedForUserAsync(userId.Value, cancellationToken);
        return Ok(notes.Select(NoteResponse.From).ToArray());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<NoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var note = await _noteService.GetByIdAsync(userId.Value, id, cancellationToken);
        if (note is null)
        {
            return NotFound();
        }

        return Ok(NoteResponse.From(note));
    }

    [HttpPost("{id:guid}/collaborators")]
    [ProducesResponseType<CollaboratorResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddCollaborator(
        Guid id,
        [FromBody] AddCollaboratorRequest request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = GetUserId();
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        CollaboratorAddResult result;
        try
        {
            result = await _noteService.AddCollaboratorAsync(
                ownerUserId.Value,
                id,
                request.Email,
                cancellationToken);
        }
        catch (UserDirectoryUnavailableException exception)
        {
            _logger.LogWarning(
                exception.InnerException,
                "User service lookup failed while adding a note collaborator.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Collaborator lookup is temporarily unavailable. Please try again later."
            });
        }

        if (result.Status == CollaboratorAddStatus.NoteNotFound)
        {
            return NotFound();
        }

        if (result.Status == CollaboratorAddStatus.UserNotFound)
        {
            return BadRequest(new { message = "The email does not belong to a registered user." });
        }

        if (result.Status == CollaboratorAddStatus.CannotAddSelf)
        {
            return BadRequest(new { message = "You cannot add yourself as a collaborator." });
        }

        if (result.Status == CollaboratorAddStatus.AlreadyCollaborator)
        {
            return Conflict(new { message = "This user is already a collaborator on the note." });
        }

        var collaborator = new CollaboratorResponse(result.CollaboratorUserId!.Value);
        return CreatedAtAction(nameof(GetCollaborators), new { id }, collaborator);
    }

    [HttpGet("{id:guid}/collaborators")]
    [ProducesResponseType<IReadOnlyList<CollaboratorResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCollaborators(Guid id, CancellationToken cancellationToken)
    {
        var ownerUserId = GetUserId();
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        var collaborators = await _noteService.GetCollaboratorsAsync(
            ownerUserId.Value,
            id,
            cancellationToken);
        if (collaborators is null)
        {
            return NotFound();
        }

        return Ok(collaborators.Select(userId => new CollaboratorResponse(userId)).ToArray());
    }

    [HttpDelete("{id:guid}/collaborators/{collaboratorUserId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCollaborator(
        Guid id,
        Guid collaboratorUserId,
        CancellationToken cancellationToken)
    {
        var ownerUserId = GetUserId();
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        var wasRemoved = await _noteService.RemoveCollaboratorAsync(
            ownerUserId.Value,
            id,
            collaboratorUserId,
            cancellationToken);
        if (!wasRemoved)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<NoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateNoteRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var note = await _noteService.UpdateAsync(
            userId.Value,
            id,
            request.Title,
            request.Content,
            cancellationToken);
        if (note is null)
        {
            return NotFound();
        }

        return Ok(NoteResponse.From(note));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var wasTrashed = await _noteService.TrashAsync(userId.Value, id, cancellationToken);
        if (!wasTrashed)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}/permanent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PermanentlyDelete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var wasDeleted = await _noteService.PermanentlyDeleteAsync(
            userId.Value,
            id,
            cancellationToken);
        if (!wasDeleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPut("{noteId:guid}/labels/{labelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AttachLabel(
        Guid noteId,
        Guid labelId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var wasAttached = await _labelService.AttachToNoteAsync(
            userId.Value,
            noteId,
            labelId,
            cancellationToken);
        if (!wasAttached)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("{noteId:guid}/labels/{labelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveLabel(
        Guid noteId,
        Guid labelId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var wasRemoved = await _labelService.RemoveFromNoteAsync(
            userId.Value,
            noteId,
            labelId,
            cancellationToken);
        if (!wasRemoved)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPatch("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        return SetArchivedStatus(id, true, cancellationToken);
    }

    [HttpPatch("{id:guid}/unarchive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Unarchive(Guid id, CancellationToken cancellationToken)
    {
        return SetArchivedStatus(id, false, cancellationToken);
    }

    [HttpPatch("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var wasRestored = await _noteService.RestoreAsync(userId.Value, id, cancellationToken);
        if (!wasRestored)
        {
            return NotFound();
        }

        return NoContent();
    }

    private async Task<IActionResult> SetArchivedStatus(
        Guid noteId,
        bool isArchived,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var wasUpdated = await _noteService.SetArchivedAsync(
            userId.Value,
            noteId,
            isArchived,
            cancellationToken);
        if (!wasUpdated)
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
