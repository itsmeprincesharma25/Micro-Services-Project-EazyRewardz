using System.Net;
using System.Net.Http.Json;
using Fundoo.Notes.Application.Abstractions;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace Fundoo.Notes.Infrastructure.Users;

public sealed class HttpUserDirectoryClient : IUserDirectoryClient
{
    private readonly HttpClient _httpClient;

    public HttpUserDirectoryClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Guid?> FindUserIdByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "internal/users/lookup",
                new UserLookupRequest(email),
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<UserLookupResponse>(
                cancellationToken: cancellationToken);

            return result?.UserId;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (!IsDependencyUnavailable(exception))
            {
                throw;
            }

            throw new UserDirectoryUnavailableException(
                "The User service is temporarily unavailable.",
                exception);
        }
    }

    private static bool IsDependencyUnavailable(Exception exception)
    {
        if (exception is HttpRequestException)
        {
            return true;
        }

        if (exception is BrokenCircuitException)
        {
            return true;
        }

        if (exception is RateLimiterRejectedException)
        {
            return true;
        }

        if (exception is TimeoutRejectedException)
        {
            return true;
        }

        return false;
    }

    private sealed record UserLookupRequest(string Email);
    private sealed record UserLookupResponse(Guid UserId);
}
