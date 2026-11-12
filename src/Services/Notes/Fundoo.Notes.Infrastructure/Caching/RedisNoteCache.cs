using System.Text.Json;
using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Fundoo.Notes.Infrastructure.Caching;

public sealed class RedisNoteCache : INoteCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisNoteCache> _logger;

    public RedisNoteCache(
        IDistributedCache distributedCache,
        ILogger<RedisNoteCache> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<Note?> GetAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var cachedNote = await _distributedCache.GetStringAsync(
                CreateKey(userId, noteId),
                cancellationToken);

            if (string.IsNullOrWhiteSpace(cachedNote))
            {
                return null;
            }

            return JsonSerializer.Deserialize<Note>(cachedNote);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Notes can still be read from PostgreSQL when the optional cache is unavailable.
            _logger.LogWarning(exception, "Redis note cache read failed. Falling back to PostgreSQL.");
            return null;
        }
    }

    public async Task SetAsync(Note note, CancellationToken cancellationToken)
    {
        try
        {
            var serializedNote = JsonSerializer.Serialize(note);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration
            };

            await _distributedCache.SetStringAsync(
                CreateKey(note.UserId, note.Id),
                serializedNote,
                cacheOptions,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis note cache write failed.");
        }
    }

    public async Task RemoveAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _distributedCache.RemoveAsync(
                CreateKey(userId, noteId),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis note cache invalidation failed.");
        }
    }

    private static string CreateKey(Guid userId, Guid noteId)
    {
        return $"notes:{userId}:item:{noteId}";
    }
}
