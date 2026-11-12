using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Features.Labels;

public sealed class LabelService
{
    private readonly ILabelRepository _labelRepository;
    private readonly INoteCache _noteCache;
    private readonly TimeProvider _timeProvider;

    public LabelService(
        ILabelRepository labelRepository,
        INoteCache noteCache,
        TimeProvider timeProvider)
    {
        _labelRepository = labelRepository;
        _noteCache = noteCache;
        _timeProvider = timeProvider;
    }

    public Task<Label?> CreateAsync(Guid userId, string name, CancellationToken cancellationToken)
    {
        var cleanedName = name.Trim();
        var label = new Label
        {
            UserId = userId,
            Name = cleanedName,
            NormalizedName = NormalizeName(cleanedName),
            CreatedAtUtc = _timeProvider.GetUtcNow()
        };

        return _labelRepository.CreateAsync(label, cancellationToken);
    }

    public Task<IReadOnlyList<Label>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _labelRepository.GetForUserAsync(userId, cancellationToken);
    }

    public async Task<LabelRenameStatus> RenameAsync(
        Guid userId,
        Guid labelId,
        string name,
        CancellationToken cancellationToken)
    {
        var cleanedName = name.Trim();
        var noteIds = await _labelRepository.GetNoteIdsForLabelAsync(
            userId,
            labelId,
            cancellationToken);
        var result = await _labelRepository.RenameAsync(
            userId,
            labelId,
            cleanedName,
            NormalizeName(cleanedName),
            cancellationToken);

        if (result == LabelRenameStatus.Renamed)
        {
            await RemoveCachedNotesAsync(userId, noteIds, cancellationToken);
        }

        return result;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid labelId, CancellationToken cancellationToken)
    {
        var noteIds = await _labelRepository.GetNoteIdsForLabelAsync(
            userId,
            labelId,
            cancellationToken);
        var wasDeleted = await _labelRepository.DeleteAsync(userId, labelId, cancellationToken);
        if (wasDeleted)
        {
            await RemoveCachedNotesAsync(userId, noteIds, cancellationToken);
        }

        return wasDeleted;
    }

    public async Task<bool> AttachToNoteAsync(
        Guid userId,
        Guid noteId,
        Guid labelId,
        CancellationToken cancellationToken)
    {
        var wasAttached = await _labelRepository.AttachToNoteAsync(
            userId,
            noteId,
            labelId,
            cancellationToken);
        if (wasAttached)
        {
            await _noteCache.RemoveAsync(userId, noteId, cancellationToken);
        }

        return wasAttached;
    }

    public async Task<bool> RemoveFromNoteAsync(
        Guid userId,
        Guid noteId,
        Guid labelId,
        CancellationToken cancellationToken)
    {
        var wasRemoved = await _labelRepository.RemoveFromNoteAsync(
            userId,
            noteId,
            labelId,
            cancellationToken);
        if (wasRemoved)
        {
            await _noteCache.RemoveAsync(userId, noteId, cancellationToken);
        }

        return wasRemoved;
    }

    private async Task RemoveCachedNotesAsync(
        Guid userId,
        IReadOnlyList<Guid> noteIds,
        CancellationToken cancellationToken)
    {
        foreach (var noteId in noteIds)
        {
            await _noteCache.RemoveAsync(userId, noteId, cancellationToken);
        }
    }

    private static string NormalizeName(string name)
    {
        return name.ToUpperInvariant();
    }
}
