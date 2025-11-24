using BacpacCompatFixer.Blazor.Models;
using BacpacCompatFixer.Blazor.Services;

namespace BacpacCompatFixer.Blazor.Examples;

/// <summary>
/// Example usage of the Purchase Verification Service for premium access management
/// </summary>
public class PremiumAccessExamples
{
    private readonly IPurchaseVerificationService _purchaseService;

    public PremiumAccessExamples(IPurchaseVerificationService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    /// <summary>
    /// Example 1: Check if user has premium access
    /// </summary>
    public async Task<bool> CheckUserPremiumAccess(string userId)
    {
        var status = await _purchaseService.VerifyPurchaseAsync(userId);
        
        // User has premium if they have an active subscription
        return status.HasPurchased && status.Status == SubscriptionStatus.Active;
    }

    /// <summary>
    /// Example 2: Get user's maximum allowed file size
    /// </summary>
    public async Task<long> GetUserMaxFileSize(string userId)
    {
        var status = await _purchaseService.VerifyPurchaseAsync(userId);
        return status.MaxFileSizeBytes;
    }

    /// <summary>
    /// Example 3: Get user's subscription details
    /// </summary>
    public async Task<string> GetUserSubscriptionSummary(string userId)
    {
        var status = await _purchaseService.VerifyPurchaseAsync(userId);

        if (!status.HasPurchased)
        {
            return "Free Tier - 500MB file limit";
        }

        return status.Status switch
        {
            SubscriptionStatus.Active => $"Premium ({status.PlanId}) - 5GB file limit",
            SubscriptionStatus.Suspended => $"Suspended ({status.PlanId}) - Premium features disabled",
            SubscriptionStatus.Unsubscribed => $"Cancelled ({status.PlanId}) - Premium features disabled",
            _ => "Free Tier - 500MB file limit"
        };
    }

    /// <summary>
    /// Example 4: Validate file upload based on user's subscription
    /// </summary>
    public async Task<(bool IsAllowed, string Message)> ValidateFileUpload(string userId, long fileSizeBytes)
    {
        var status = await _purchaseService.VerifyPurchaseAsync(userId);

        if (fileSizeBytes > status.MaxFileSizeBytes)
        {
            var maxSizeMB = status.MaxFileSizeBytes / (1024 * 1024);
            var fileSizeMB = fileSizeBytes / (1024 * 1024);
            
            return (false, $"File size ({fileSizeMB}MB) exceeds your limit ({maxSizeMB}MB). " +
                          (status.HasPurchased ? "" : "Upgrade to Premium for 5GB file limit."));
        }

        return (true, "File size is within your limit.");
    }

    /// <summary>
    /// Example 5: Check if user's subscription is active
    /// </summary>
    public async Task<bool> IsSubscriptionActive(string userId)
    {
        var status = await _purchaseService.VerifyPurchaseAsync(userId);
        return status.Status == SubscriptionStatus.Active;
    }

    /// <summary>
    /// Example 6: Get subscription status message for UI display
    /// </summary>
    public async Task<string> GetSubscriptionStatusMessage(string userId)
    {
        var status = await _purchaseService.VerifyPurchaseAsync(userId);

        return status.Status switch
        {
            SubscriptionStatus.Free => "You are using the free tier. Upgrade to Premium for more features!",
            SubscriptionStatus.Active => $"Your Premium subscription is active. Plan: {status.PlanId}",
            SubscriptionStatus.Suspended => "Your subscription is suspended. Please update your payment method.",
            SubscriptionStatus.Unsubscribed => "Your subscription has been cancelled. You can reactivate anytime.",
            _ => "Subscription status unknown."
        };
    }

    /// <summary>
    /// Example 7: Manually grant premium access (for testing)
    /// </summary>
    public async Task GrantPremiumAccessForTesting(string userId, string testPlanId = "test-premium")
    {
        await _purchaseService.UpdateSubscriptionAsync(
            subscriptionId: $"test-sub-{Guid.NewGuid()}",
            planId: testPlanId,
            status: SubscriptionStatus.Active,
            userEmail: userId
        );
    }

    /// <summary>
    /// Example 8: Check if a specific plan is premium
    /// </summary>
    public bool CheckIfPlanIsPremium(string planId)
    {
        return _purchaseService.IsPremiumPlan(planId);
    }

    /// <summary>
    /// Example 9: Get subscription by subscription ID
    /// </summary>
    public async Task<UserPurchaseStatus?> GetSubscriptionDetails(string subscriptionId)
    {
        return await _purchaseService.GetSubscriptionByIdAsync(subscriptionId);
    }

    /// <summary>
    /// Example 10: Feature gating based on subscription
    /// </summary>
    public async Task<T?> ExecutePremiumFeature<T>(string userId, Func<Task<T>> premiumAction, T? freeAlternative = default)
    {
        var hasPremium = await CheckUserPremiumAccess(userId);

        if (hasPremium)
        {
            return await premiumAction();
        }

        return freeAlternative;
    }
}

/// <summary>
/// Example API controller showing premium feature access control
/// </summary>
/*
[ApiController]
[Route("api/[controller]")]
public class BacpacProcessingController : ControllerBase
{
    private readonly IPurchaseVerificationService _purchaseService;

    public BacpacProcessingController(IPurchaseVerificationService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        // Get user ID from claims
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check user's subscription
        var status = await _purchaseService.VerifyPurchaseAsync(userId);

        // Validate file size
        if (file.Length > status.MaxFileSizeBytes)
        {
            var maxSizeMB = status.MaxFileSizeBytes / (1024 * 1024);
            return BadRequest(new 
            { 
                error = $"File too large. Your limit is {maxSizeMB}MB.",
                requiresPremium = !status.HasPurchased
            });
        }

        // Process file
        // ... processing logic ...

        return Ok(new { message = "File uploaded successfully" });
    }

    [HttpGet("features")]
    public async Task<IActionResult> GetAvailableFeatures()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var status = await _purchaseService.VerifyPurchaseAsync(userId);
        var isPremium = status.HasPurchased && status.Status == SubscriptionStatus.Active;

        return Ok(new
        {
            tier = isPremium ? "Premium" : "Free",
            maxFileSize = status.MaxFileSizeBytes,
            features = new
            {
                basicProcessing = true,
                largeFiles = isPremium,
                batchProcessing = isPremium,
                prioritySupport = isPremium,
                advancedFeatures = isPremium
            }
        });
    }
}
*/
