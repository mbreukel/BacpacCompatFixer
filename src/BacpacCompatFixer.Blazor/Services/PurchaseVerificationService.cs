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
    private readonly ILogger<PurchaseVerificationService> _logger;
    
    // Default limits
    private const long FreeTierMaxFileSize = 500 * 1024 * 1024; // 500 MB
    private const long PremiumTierMaxFileSize = 5L * 1024 * 1024 * 1024; // 5 GB

    public PurchaseVerificationService(IWebHostEnvironment environment, ILogger<PurchaseVerificationService> logger)
    {
        _logger = logger;
        _storageDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "Purchases");
        Directory.CreateDirectory(_storageDirectory);
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
                MaxFileSizeBytes = FreeTierMaxFileSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying purchase for user {UserId}", userId);
            return new UserPurchaseStatus
            {
                UserId = userId,
                HasPurchased = false,
                MaxFileSizeBytes = FreeTierMaxFileSize
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
                TransactionId = transactionId
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

    private string GetUserPurchaseFilePath(string userId)
    {
        // Sanitize userId for file name
        var sanitizedUserId = string.Concat(userId.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        if (string.IsNullOrEmpty(sanitizedUserId))
        {
            throw new ArgumentException("UserId must contain at least one valid character (letter, digit, '-' or '_') after sanitization.", nameof(userId));
        }
        return Path.Combine(_storageDirectory, $"{sanitizedUserId}.json");
    }
}
