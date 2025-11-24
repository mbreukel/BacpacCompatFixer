using System.Text.Json;
using BacpacCompatFixer.Blazor.Models;

namespace BacpacCompatFixer.Blazor.Services;

/// <summary>
/// Implementation of purchase verification service
/// Uses file-based storage for demo purposes. In production, this would integrate with Microsoft Marketplace API
/// </summary>
public class PurchaseVerificationService : IPurchaseVerificationService
{
    private readonly string _storageDirectory;
    private readonly string _subscriptionIndexDirectory;
    private readonly ILogger<PurchaseVerificationService> _logger;
    private readonly IConfiguration _configuration;
    
    // Default limits
    private const long FreeTierMaxFileSize = 500 * 1024 * 1024; // 500 MB
    private const long PremiumTierMaxFileSize = 5L * 1024 * 1024 * 1024; // 5 GB

    public PurchaseVerificationService(
        IWebHostEnvironment environment, 
        ILogger<PurchaseVerificationService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _storageDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "Purchases");
        _subscriptionIndexDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "Subscriptions");
        Directory.CreateDirectory(_storageDirectory);
        Directory.CreateDirectory(_subscriptionIndexDirectory);
    }

    public async Task<UserPurchaseStatus> VerifyPurchaseAsync(string userId)
    {
        try
        {
            var filePath = GetUserPurchaseFilePath(userId);
            
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                var status = JsonSerializer.Deserialize<UserPurchaseStatus>(json);
                if (status != null)
                {
                    return status;
                }
            }

            // Return free tier status if no purchase record found
            return new UserPurchaseStatus
            {
                UserId = userId,
                HasPurchased = false,
                MaxFileSizeBytes = FreeTierMaxFileSize,
                Status = SubscriptionStatus.Free
            };
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error reading purchase file for user {UserId}", userId);
            return new UserPurchaseStatus
            {
                UserId = userId,
                HasPurchased = false,
                MaxFileSizeBytes = FreeTierMaxFileSize,
                Status = SubscriptionStatus.Free
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing purchase data for user {UserId}", userId);
            return new UserPurchaseStatus
            {
                UserId = userId,
                HasPurchased = false,
                MaxFileSizeBytes = FreeTierMaxFileSize,
                Status = SubscriptionStatus.Free
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied to purchase file for user {UserId}", userId);
            return new UserPurchaseStatus
            {
                UserId = userId,
                HasPurchased = false,
                MaxFileSizeBytes = FreeTierMaxFileSize,
                Status = SubscriptionStatus.Free
            };
        }
    }

    public async Task RecordPurchaseAsync(string userId, string transactionId)
    {
        try
        {
            var status = new UserPurchaseStatus
            {
                UserId = userId,
                HasPurchased = true,
                PurchaseDate = DateTime.UtcNow,
                MaxFileSizeBytes = PremiumTierMaxFileSize,
                TransactionId = transactionId,
                Status = SubscriptionStatus.Active,
                LastUpdated = DateTime.UtcNow
            };

            var filePath = GetUserPurchaseFilePath(userId);
            var json = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInformation("Purchase recorded for user {UserId} with transaction {TransactionId}", userId, transactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording purchase for user {UserId}", userId);
            throw;
        }
    }

    public async Task UpdateSubscriptionAsync(string subscriptionId, string planId, SubscriptionStatus status, string? userEmail = null)
    {
        try
        {
            // First, try to find existing subscription
            var existingStatus = await GetSubscriptionByIdAsync(subscriptionId);
            
            if (existingStatus != null)
            {
                // Update existing subscription
                existingStatus.PlanId = planId;
                existingStatus.Status = status;
                existingStatus.LastUpdated = DateTime.UtcNow;
                existingStatus.HasPurchased = status == SubscriptionStatus.Active && IsPremiumPlan(planId);
                existingStatus.MaxFileSizeBytes = existingStatus.HasPurchased ? PremiumTierMaxFileSize : FreeTierMaxFileSize;

                // Update user email if provided
                if (!string.IsNullOrEmpty(userEmail))
                {
                    existingStatus.Email = userEmail;
                }

                await SaveUserPurchaseStatusAsync(existingStatus);
                _logger.LogInformation("Updated subscription {SubscriptionId} to plan {PlanId} with status {Status}", 
                    subscriptionId, planId, status);
            }
            else
            {
                // Create new subscription record
                var newStatus = new UserPurchaseStatus
                {
                    UserId = userEmail ?? subscriptionId, // Use email as UserId or subscriptionId as fallback
                    Email = userEmail ?? string.Empty,
                    SubscriptionId = subscriptionId,
                    PlanId = planId,
                    Status = status,
                    HasPurchased = status == SubscriptionStatus.Active && IsPremiumPlan(planId),
                    PurchaseDate = DateTime.UtcNow,
                    MaxFileSizeBytes = (status == SubscriptionStatus.Active && IsPremiumPlan(planId)) ? PremiumTierMaxFileSize : FreeTierMaxFileSize,
                    LastUpdated = DateTime.UtcNow
                };

                await SaveUserPurchaseStatusAsync(newStatus);
                _logger.LogInformation("Created new subscription {SubscriptionId} with plan {PlanId} and status {Status}", 
                    subscriptionId, planId, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public async Task<UserPurchaseStatus?> GetSubscriptionByIdAsync(string subscriptionId)
    {
        try
        {
            // Check subscription index
            var indexPath = GetSubscriptionIndexFilePath(subscriptionId);
            
            if (File.Exists(indexPath))
            {
                var userId = await File.ReadAllTextAsync(indexPath);
                var userFilePath = GetUserPurchaseFilePath(userId.Trim());
                
                if (File.Exists(userFilePath))
                {
                    var json = await File.ReadAllTextAsync(userFilePath);
                    return JsonSerializer.Deserialize<UserPurchaseStatus>(json);
                }
            }

            // Fallback: Search all user files for subscription ID
            var allFiles = Directory.GetFiles(_storageDirectory, "*.json");
            foreach (var file in allFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var status = JsonSerializer.Deserialize<UserPurchaseStatus>(json);
                    
                    if (status?.SubscriptionId == subscriptionId)
                    {
                        // Update index
                        await File.WriteAllTextAsync(indexPath, status.UserId);
                        return status;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading file {File}", file);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription {SubscriptionId}", subscriptionId);
            return null;
        }
    }

    public bool IsPremiumPlan(string planId)
    {
        if (string.IsNullOrWhiteSpace(planId))
            return false;

        // Get premium plan IDs from configuration
        var premiumPlans = _configuration.GetSection("Marketplace:PremiumPlanIds").Get<string[]>() 
            ?? new[] { "premium", "pro", "professional", "enterprise" };

        return premiumPlans.Any(p => planId.Equals(p, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SaveUserPurchaseStatusAsync(UserPurchaseStatus status)
    {
        var filePath = GetUserPurchaseFilePath(status.UserId);
        var json = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);

        // Update subscription index if subscription ID exists
        if (!string.IsNullOrEmpty(status.SubscriptionId))
        {
            var indexPath = GetSubscriptionIndexFilePath(status.SubscriptionId);
            await File.WriteAllTextAsync(indexPath, status.UserId);
        }
    }

    private string GetUserPurchaseFilePath(string userId)
    {
        // Sanitize userId for file name
        var sanitizedUserId = string.Concat(userId.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '@' || c == '.'));
        if (string.IsNullOrEmpty(sanitizedUserId))
        {
            throw new ArgumentException("UserId must contain at least one valid character after sanitization.", nameof(userId));
        }
        return Path.Combine(_storageDirectory, $"{sanitizedUserId}.json");
    }

    private string GetSubscriptionIndexFilePath(string subscriptionId)
    {
        var sanitizedId = string.Concat(subscriptionId.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        return Path.Combine(_subscriptionIndexDirectory, $"{sanitizedId}.txt");
    }
}
