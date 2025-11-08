namespace BacpacCompatFixer.Blazor.Models;

/// <summary>
/// Represents the purchase status for a user
/// </summary>
public class UserPurchaseStatus
{
    /// <summary>
    /// User ID (from Azure AD)
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user has purchased the premium version
    /// </summary>
    public bool HasPurchased { get; set; }

    /// <summary>
    /// Date of purchase
    /// </summary>
    public DateTime? PurchaseDate { get; set; }

    /// <summary>
    /// Maximum file size allowed in bytes
    /// </summary>
    public long MaxFileSizeBytes { get; set; }

    /// <summary>
    /// Purchase transaction ID from marketplace
    /// </summary>
    public string? TransactionId { get; set; }
}
