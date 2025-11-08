namespace BacpacCompatFixer.Blazor.Services;

/// <summary>
/// Service to implement rate limiting for file uploads
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Check if the user can perform an upload
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>True if allowed, false if rate limited</returns>
    Task<bool> CanUploadAsync(string userId);

    /// <summary>
    /// Record an upload attempt
    /// </summary>
    /// <param name="userId">User ID</param>
    Task RecordUploadAsync(string userId);

    /// <summary>
    /// Get the time until the next allowed upload
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>TimeSpan until next allowed upload, or null if can upload now</returns>
    Task<TimeSpan?> GetTimeUntilNextUploadAsync(string userId);
}
