using System.Text.Json.Serialization;

namespace BacpacCompatFixer.Blazor.Models;

/// <summary>
/// Represents a webhook event from Microsoft Marketplace
/// </summary>
public class MarketplaceWebhookEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("activityId")]
    public string ActivityId { get; set; } = string.Empty;

    [JsonPropertyName("subscriptionId")]
    public string SubscriptionId { get; set; } = string.Empty;

    [JsonPropertyName("offerId")]
    public string OfferId { get; set; } = string.Empty;

    [JsonPropertyName("planId")]
    public string PlanId { get; set; } = string.Empty;

    [JsonPropertyName("publisherId")]
    public string PublisherId { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("timeStamp")]
    public DateTime TimeStamp { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("purchaser")]
    public PurchaserInfo? Purchaser { get; set; }
}

public class PurchaserInfo
{
    [JsonPropertyName("emailId")]
    public string EmailId { get; set; } = string.Empty;

    [JsonPropertyName("objectId")]
    public string ObjectId { get; set; } = string.Empty;

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("puid")]
    public string Puid { get; set; } = string.Empty;
}
