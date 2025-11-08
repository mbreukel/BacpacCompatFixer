using System.Collections.Concurrent;

namespace BacpacCompatFixer.Blazor.Services;

/// <summary>
/// Implementation of rate limiting service
/// Uses in-memory storage for simplicity. In production, use distributed cache (Redis)
/// </summary>
public class RateLimitService : IRateLimitService
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<DateTime>> _uploadAttempts = new();
    private readonly ILogger<RateLimitService> _logger;
    
    // Rate limit configuration
    private const int MaxUploadsPerHour = 10; // Free tier: 10 uploads per hour
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);

    public RateLimitService(ILogger<RateLimitService> logger)
    {
        _logger = logger;
    }

    public Task<bool> CanUploadAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(false);
        }

        var now = DateTime.UtcNow;
        var attempts = GetRecentAttempts(userId, now);

        // For now, use free tier limit for all users
        // In production, check purchase status and adjust limit accordingly
        var allowed = attempts.Count < MaxUploadsPerHour;

        if (!allowed)
        {
            _logger.LogWarning("Rate limit exceeded for user {UserId}. Attempts: {Count}", userId, attempts.Count);
        }

        return Task.FromResult(allowed);
    }

    public Task RecordUploadAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.CompletedTask;
        }

        var now = DateTime.UtcNow;
        _uploadAttempts.AddOrUpdate(
            userId,
            _ => new ConcurrentBag<DateTime> { now },
            (_, attempts) =>
            {
                attempts.Add(now);
                return attempts;
            }
        );

        _logger.LogInformation("Upload recorded for user {UserId}", userId);
        return Task.CompletedTask;
    }

    public Task<TimeSpan?> GetTimeUntilNextUploadAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult<TimeSpan?>(null);
        }

        var now = DateTime.UtcNow;
        var attempts = GetRecentAttempts(userId, now);

        if (attempts.Count < MaxUploadsPerHour)
        {
            return Task.FromResult<TimeSpan?>(null);
        }

        // Find the oldest attempt within the window
        var oldestAttempt = attempts.Min();
        var timeUntilReset = (oldestAttempt + RateLimitWindow) - now;

        return Task.FromResult<TimeSpan?>(timeUntilReset > TimeSpan.Zero ? timeUntilReset : null);
    }

    private List<DateTime> GetRecentAttempts(string userId, DateTime now)
    {
        if (!_uploadAttempts.TryGetValue(userId, out var attempts))
        {
            return new List<DateTime>();
        }

        // Return only recent attempts within the window
        return attempts.Where(t => now - t <= RateLimitWindow).ToList();
    }
}
