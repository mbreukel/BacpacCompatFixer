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
}
