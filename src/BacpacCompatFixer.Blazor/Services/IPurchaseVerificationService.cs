using BacpacCompatFixer.Blazor.Models;

namespace BacpacCompatFixer.Blazor.Services;

/// <summary>
/// Service for verifying user purchase status from Microsoft Marketplace
/// </summary>
public interface IPurchaseVerificationService
{
    /// <summary>
    /// Verify if a user has purchased the premium version
    /// </summary>
    /// <param name="userId">User ID from Azure AD</param>
    /// <returns>Purchase status</returns>
    Task<UserPurchaseStatus> VerifyPurchaseAsync(string userId);

    /// <summary>
    /// Record a new purchase
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="transactionId">Transaction ID from marketplace</param>
    Task RecordPurchaseAsync(string userId, string transactionId);

    /// <summary>
    /// Update subscription status for a user
    /// </summary>
    /// <param name="subscriptionId">Subscription ID from marketplace</param>
    /// <param name="planId">Plan ID</param>
    /// <param name="status">Subscription status</param>
    /// <param name="userEmail">User email address</param>
    Task UpdateSubscriptionAsync(string subscriptionId, string planId, SubscriptionStatus status, string? userEmail = null);

    /// <summary>
    /// Get subscription by subscription ID
    /// </summary>
    /// <param name="subscriptionId">Subscription ID from marketplace</param>
    /// <returns>User purchase status or null if not found</returns>
    Task<UserPurchaseStatus?> GetSubscriptionByIdAsync(string subscriptionId);

    /// <summary>
    /// Check if a plan ID is a premium plan
    /// </summary>
    /// <param name="planId">Plan ID to check</param>
    /// <returns>True if premium plan</returns>
    bool IsPremiumPlan(string planId);
}
