using BacpacCompatFixer.Blazor.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BacpacCompatFixer.Blazor.Services;

/// <summary>
/// Real-time purchase verification using Microsoft Marketplace API
/// No local file storage - queries API on every check with optional short-term caching
/// </summary>
public class RealTimePurchaseVerificationService : IPurchaseVerificationService
{
    private readonly IMarketplaceApiService _marketplaceApi;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RealTimePurchaseVerificationService> _logger;

    private const long FreeTierMaxFileSize = 500 * 1024 * 1024; // 500 MB
    private const long PremiumTierMaxFileSize = 5L * 1024 * 1024 * 1024; // 5 GB

    public RealTimePurchaseVerificationService(
        IMarketplaceApiService marketplaceApi,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<RealTimePurchaseVerificationService> logger)
    {
        _marketplaceApi = marketplaceApi;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UserPurchaseStatus> VerifyPurchaseAsync(string userId)
    {
        var cacheKey = $"purchase_status_{userId}";
        var enableCaching = _configuration.GetValue<bool>("Marketplace:EnableCaching", true);
        var cacheDurationMinutes = _configuration.GetValue<int>("Marketplace:CacheDurationMinutes", 5);

        // Check cache first if caching is enabled
        if (enableCaching && _cache.TryGetValue<UserPurchaseStatus>(cacheKey, out var cachedStatus))
        {
            _logger.LogDebug("Returning cached purchase status for {UserId} (cached until {CacheExpiry})", 
                userId, DateTime.UtcNow.AddMinutes(cacheDurationMinutes));
            return cachedStatus!;
        }

        try
        {
            // Query Microsoft Marketplace API in real-time
            _logger.LogInformation("Querying Marketplace API in real-time for user {UserId}", userId);
            var subscription = await _marketplaceApi.GetSubscriptionByUserEmailAsync(userId);

            UserPurchaseStatus status;

            if (subscription != null && subscription.SaasSubscriptionStatus == "Subscribed")
            {
                var isPremium = IsPremiumPlan(subscription.PlanId);
                
                status = new UserPurchaseStatus
                {
                    UserId = userId,
                    Email = subscription.Beneficiary?.EmailId ?? userId,
                    HasPurchased = isPremium,
                    SubscriptionId = subscription.Id,
                    PlanId = subscription.PlanId,
                    Status = isPremium ? SubscriptionStatus.Active : SubscriptionStatus.Free,
                    MaxFileSizeBytes = isPremium ? PremiumTierMaxFileSize : FreeTierMaxFileSize,
                    PurchaseDate = subscription.Term?.StartDate,
                    LastUpdated = DateTime.UtcNow
                };

                _logger.LogInformation("User {UserId} has {Status} subscription with plan {PlanId} (Premium: {IsPremium})", 
                    userId, status.Status, subscription.PlanId, isPremium);
            }
            else if (subscription != null)
            {
                // Subscription exists but not active
                var subscriptionStatus = MapApiStatusToSubscriptionStatus(subscription.SaasSubscriptionStatus);
                
                status = new UserPurchaseStatus
                {
                    UserId = userId,
                    Email = subscription.Beneficiary?.EmailId ?? userId,
                    HasPurchased = false,
                    SubscriptionId = subscription.Id,
                    PlanId = subscription.PlanId,
                    Status = subscriptionStatus,
                    MaxFileSizeBytes = FreeTierMaxFileSize,
                    PurchaseDate = subscription.Term?.StartDate,
                    LastUpdated = DateTime.UtcNow
                };

                _logger.LogInformation("User {UserId} has subscription {SubscriptionId} with status {ApiStatus} - no premium access", 
                    userId, subscription.Id, subscription.SaasSubscriptionStatus);
            }
            else
            {
                // No subscription found
                status = new UserPurchaseStatus
                {
                    UserId = userId,
                    Email = userId,
                    HasPurchased = false,
                    Status = SubscriptionStatus.Free,
                    MaxFileSizeBytes = FreeTierMaxFileSize,
                    LastUpdated = DateTime.UtcNow
                };

                _logger.LogInformation("User {UserId} has no active subscription - Free tier", userId);
            }

            // Cache the result if caching is enabled
            if (enableCaching)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheDurationMinutes)
                };
                _cache.Set(cacheKey, status, cacheOptions);
                _logger.LogDebug("Cached purchase status for {UserId} for {Minutes} minutes", userId, cacheDurationMinutes);
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying purchase for user {UserId} via Marketplace API", userId);
            
            // Return free tier on error (fail-safe)
            return new UserPurchaseStatus
            {
                UserId = userId,
                Email = userId,
                HasPurchased = false,
                Status = SubscriptionStatus.Free,
                MaxFileSizeBytes = FreeTierMaxFileSize,
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    public async Task<UserPurchaseStatus?> GetSubscriptionByIdAsync(string subscriptionId)
    {
        try
        {
            _logger.LogDebug("Fetching subscription {SubscriptionId} from Marketplace API", subscriptionId);
            
            var subscription = await _marketplaceApi.GetSubscriptionByIdAsync(subscriptionId);
            
            if (subscription == null)
            {
                _logger.LogInformation("Subscription {SubscriptionId} not found in Marketplace", subscriptionId);
                return null;
            }

            var isPremium = IsPremiumPlan(subscription.PlanId);
            var isActive = subscription.SaasSubscriptionStatus == "Subscribed";
            
            return new UserPurchaseStatus
            {
                UserId = subscription.Beneficiary?.EmailId ?? subscription.Id,
                Email = subscription.Beneficiary?.EmailId ?? string.Empty,
                SubscriptionId = subscription.Id,
                PlanId = subscription.PlanId,
                HasPurchased = isPremium && isActive,
                Status = MapApiStatusToSubscriptionStatus(subscription.SaasSubscriptionStatus, isPremium),
                MaxFileSizeBytes = (isPremium && isActive) ? PremiumTierMaxFileSize : FreeTierMaxFileSize,
                PurchaseDate = subscription.Term?.StartDate,
                LastUpdated = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription {SubscriptionId} from Marketplace API", subscriptionId);
            return null;
        }
    }

    public async Task UpdateSubscriptionAsync(string subscriptionId, string planId, SubscriptionStatus status, string? userEmail = null)
    {
        // When webhook is received, invalidate cache for this user
        if (!string.IsNullOrEmpty(userEmail))
        {
            var cacheKey = $"purchase_status_{userEmail}";
            _cache.Remove(cacheKey);
            _logger.LogInformation("Cache invalidated for user {Email} after webhook event - next login will query fresh data from API", userEmail);
        }

        // Also try to find and invalidate by subscription ID
        try
        {
            var subscription = await _marketplaceApi.GetSubscriptionByIdAsync(subscriptionId);
            if (subscription?.Beneficiary?.EmailId != null)
            {
                var beneficiaryCacheKey = $"purchase_status_{subscription.Beneficiary.EmailId}";
                _cache.Remove(beneficiaryCacheKey);
                _logger.LogInformation("Cache invalidated for beneficiary {Email} of subscription {SubscriptionId}", 
                    subscription.Beneficiary.EmailId, subscriptionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not invalidate cache by subscription ID {SubscriptionId}", subscriptionId);
        }

        await Task.CompletedTask;
    }

    public Task RecordPurchaseAsync(string userId, string transactionId)
    {
        // Not needed for API-based implementation
        // Purchases are recorded by Microsoft Marketplace
        _logger.LogInformation("Purchase recording requested for {UserId} with transaction {TransactionId} - handled by Marketplace", 
            userId, transactionId);
        
        // Invalidate cache to force refresh on next check
        var cacheKey = $"purchase_status_{userId}";
        _cache.Remove(cacheKey);
        
        return Task.CompletedTask;
    }

    public bool IsPremiumPlan(string planId)
    {
        if (string.IsNullOrWhiteSpace(planId))
            return false;

        var premiumPlans = _configuration.GetSection("Marketplace:PremiumPlanIds").Get<string[]>() 
            ?? new[] { "premium", "pro", "professional", "enterprise" };

        var isPremium = premiumPlans.Any(p => planId.Equals(p, StringComparison.OrdinalIgnoreCase));
        
        _logger.LogDebug("Plan {PlanId} is {PremiumStatus}", planId, isPremium ? "PREMIUM" : "NOT premium");
        
        return isPremium;
    }

    private SubscriptionStatus MapApiStatusToSubscriptionStatus(string apiStatus, bool isPremium = false)
    {
        return apiStatus.ToLower() switch
        {
            "subscribed" => isPremium ? SubscriptionStatus.Active : SubscriptionStatus.Free,
            "suspended" => SubscriptionStatus.Suspended,
            "unsubscribed" => SubscriptionStatus.Unsubscribed,
            "pendingfulfillmentstart" => SubscriptionStatus.Free, // Not yet activated
            _ => SubscriptionStatus.Free
        };
    }
}
