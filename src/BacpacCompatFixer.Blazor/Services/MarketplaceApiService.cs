using System.Text.Json.Serialization;

namespace BacpacCompatFixer.Blazor.Services;

/// <summary>
/// Service for interacting with Microsoft Marketplace Fulfillment APIs
/// </summary>
public interface IMarketplaceApiService
{
    /// <summary>
    /// Get all subscriptions for this publisher
    /// </summary>
    Task<List<MarketplaceSubscription>> GetAllSubscriptionsAsync();

    /// <summary>
    /// Get subscription by ID
    /// </summary>
    Task<MarketplaceSubscription?> GetSubscriptionByIdAsync(string subscriptionId);

    /// <summary>
    /// Get active subscription for a specific user (by email)
    /// </summary>
    Task<MarketplaceSubscription?> GetSubscriptionByUserEmailAsync(string userEmail);
}

/// <summary>
/// Implements Microsoft Marketplace SaaS Fulfillment API v2 calls
/// </summary>
public class MarketplaceApiService : IMarketplaceApiService
{
    private readonly IMarketplaceAuthService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MarketplaceApiService> _logger;
    private const string MarketplaceApiBaseUrl = "https://marketplaceapi.microsoft.com/api/saas";
    private const string ApiVersion = "2018-08-31";

    public MarketplaceApiService(
        IMarketplaceAuthService authService,
        IHttpClientFactory httpClientFactory,
        ILogger<MarketplaceApiService> logger)
    {
        _authService = authService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<MarketplaceSubscription>> GetAllSubscriptionsAsync()
    {
        try
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            var httpClient = _httpClientFactory.CreateClient();

            var url = $"{MarketplaceApiBaseUrl}/subscriptions?api-version={ApiVersion}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Headers.Add("Accept", "application/json");

            _logger.LogDebug("Fetching all subscriptions from Marketplace API");

            var response = await httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to retrieve subscriptions. Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, errorContent);
                return new List<MarketplaceSubscription>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<SubscriptionsListResponse>(content);

            var subscriptions = result?.Subscriptions ?? new List<MarketplaceSubscription>();
            _logger.LogInformation("Retrieved {Count} subscriptions from Marketplace API", subscriptions.Count);
            
            return subscriptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscriptions from Marketplace API");
            return new List<MarketplaceSubscription>();
        }
    }

    public async Task<MarketplaceSubscription?> GetSubscriptionByIdAsync(string subscriptionId)
    {
        try
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            var httpClient = _httpClientFactory.CreateClient();

            var url = $"{MarketplaceApiBaseUrl}/subscriptions/{subscriptionId}?api-version={ApiVersion}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Headers.Add("Accept", "application/json");

            _logger.LogDebug("Fetching subscription {SubscriptionId} from Marketplace API", subscriptionId);

            var response = await httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Subscription {SubscriptionId} not found", subscriptionId);
                }
                else
                {
                    _logger.LogWarning("Failed to retrieve subscription {SubscriptionId}. Status: {StatusCode}", 
                        subscriptionId, response.StatusCode);
                }
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var subscription = System.Text.Json.JsonSerializer.Deserialize<MarketplaceSubscription>(content);

            _logger.LogInformation("Retrieved subscription {SubscriptionId} with status {Status}", 
                subscriptionId, subscription?.SaasSubscriptionStatus);

            return subscription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscription {SubscriptionId}", subscriptionId);
            return null;
        }
    }

    public async Task<MarketplaceSubscription?> GetSubscriptionByUserEmailAsync(string userEmail)
    {
        try
        {
            _logger.LogDebug("Searching for active subscription for user {Email}", userEmail);
            
            var allSubscriptions = await GetAllSubscriptionsAsync();
            
            // Find active subscription for this user (as beneficiary or purchaser)
            var subscription = allSubscriptions.FirstOrDefault(s => 
                (s.Beneficiary?.EmailId?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true ||
                 s.Purchaser?.EmailId?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true) &&
                s.SaasSubscriptionStatus == "Subscribed");

            if (subscription != null)
            {
                _logger.LogInformation("Found active subscription for user {Email}: {SubscriptionId} with plan {PlanId}", 
                    userEmail, subscription.Id, subscription.PlanId);
            }
            else
            {
                _logger.LogInformation("No active subscription found for user {Email}", userEmail);
            }

            return subscription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding subscription for user {Email}", userEmail);
            return null;
        }
    }

    private class SubscriptionsListResponse
    {
        [JsonPropertyName("subscriptions")]
        public List<MarketplaceSubscription>? Subscriptions { get; set; }

        [JsonPropertyName("@nextLink")]
        public string? NextLink { get; set; }
    }
}

/// <summary>
/// Represents a subscription from Microsoft Marketplace API
/// </summary>
public class MarketplaceSubscription
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("publisherId")]
    public string PublisherId { get; set; } = string.Empty;

    [JsonPropertyName("offerId")]
    public string OfferId { get; set; } = string.Empty;

    [JsonPropertyName("planId")]
    public string PlanId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("beneficiary")]
    public SubscriptionUser? Beneficiary { get; set; }

    [JsonPropertyName("purchaser")]
    public SubscriptionUser? Purchaser { get; set; }

    [JsonPropertyName("saasSubscriptionStatus")]
    public string SaasSubscriptionStatus { get; set; } = string.Empty;

    [JsonPropertyName("term")]
    public SubscriptionTerm? Term { get; set; }

    [JsonPropertyName("autoRenew")]
    public bool AutoRenew { get; set; }

    [JsonPropertyName("isFreeTrial")]
    public bool IsFreeTrial { get; set; }

    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }
}

/// <summary>
/// Represents a user (beneficiary or purchaser) in a subscription
/// </summary>
public class SubscriptionUser
{
    [JsonPropertyName("emailId")]
    public string? EmailId { get; set; }

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; set; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    [JsonPropertyName("puid")]
    public string? Puid { get; set; }
}

/// <summary>
/// Represents the subscription term details
/// </summary>
public class SubscriptionTerm
{
    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("termUnit")]
    public string? TermUnit { get; set; }
}
