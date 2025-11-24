# Microsoft Marketplace Integration Guide

This guide explains how to integrate the BacpacCompatFixer with Microsoft Commercial Marketplace for purchase verification.

## Overview

The Microsoft Commercial Marketplace allows you to publish and monetize your SaaS application. This guide covers:
1. Publishing your app to the marketplace
2. Implementing SaaS fulfillment APIs
3. Handling purchase webhooks
4. Verifying purchase status

## Prerequisites

- Microsoft Partner Center account
- Azure subscription
- Published Azure AD application
- Live BacpacCompatFixer deployment

## Step 1: Register in Partner Center

1. Go to [Partner Center](https://partner.microsoft.com/dashboard)
2. Sign in with your Microsoft account
3. Navigate to **Commercial Marketplace** → **Overview**
4. Click **+ New offer** → **Software as a Service (SaaS)**
5. Enter offer ID: `bacpacfixer`
6. Click **Create**

## Step 2: Configure Offer Setup

### Technical Configuration

1. **Landing page URL**: `https://yourdomain.com/marketplace/landing`
2. **Connection webhook URL**: `https://yourdomain.com/api/marketplace/webhook`
3. **Azure Active Directory tenant ID**: Your Azure AD tenant ID
4. **Azure Active Directory application ID**: Your app's client ID

### Offer Listing

- **Name**: BacpacCompatFixer Premium
- **Summary**: Process large .bacpac files (up to 5 GB) to fix SQL Server compatibility issues
- **Description**: Comprehensive description of your service
- **Support URLs**: Links to documentation and support
- **Privacy policy URL**: Your privacy policy
- **Screenshots**: Add application screenshots

## Step 3: Create Plans

Create at least one plan (pricing tier):

### Free Trial Plan
- **Plan ID**: `free-trial`
- **Plan name**: Free Trial
- **Duration**: 30 days
- **Price**: Free

### Premium Plan
- **Plan ID**: `premium`
- **Plan name**: Premium
- **Billing term**: Monthly
- **Price**: $9.99/month (example)
- **Features**:
  - Upload files up to 5 GB
  - 50 uploads per hour
  - Priority support

## Step 4: Implement SaaS Fulfillment APIs

The marketplace will call your application via webhooks. You need to implement:

### Landing Page Endpoint

Create a new controller `MarketplaceController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace BacpacCompatFixer.Blazor.Controllers;

[Route("marketplace")]
public class MarketplaceController : Controller
{
    private readonly ILogger<MarketplaceController> _logger;
    private readonly IPurchaseVerificationService _purchaseService;

    public MarketplaceController(
        ILogger<MarketplaceController> logger,
        IPurchaseVerificationService purchaseService)
    {
        _logger = logger;
        _purchaseService = purchaseService;
    }

    [HttpGet("landing")]
    [AuthorizeForScopes(Scopes = new[] { "User.Read" })]
    public async Task<IActionResult> Landing(
        [FromQuery] string token,
        [FromQuery] string? error = null,
        [FromQuery] string? error_description = null)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("Marketplace landing error: {Error} - {Description}", error, error_description);
            return View("Error", new { error, error_description });
        }

        try
        {
            // Resolve the marketplace token to get subscription details
            var subscription = await ResolveMarketplaceTokenAsync(token);
            
            // Get current user
            var userId = User.FindFirst("oid")?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found");
            }

            // Record the purchase
            await _purchaseService.RecordPurchaseAsync(userId, subscription.Id);

            // Activate the subscription
            await ActivateSubscriptionAsync(subscription.Id);

            _logger.LogInformation("Marketplace subscription activated for user {UserId}", userId);

            // Redirect to success page
            return Redirect("/marketplace/success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process marketplace landing");
            return View("Error", new { error = "processing_error", error_description = ex.Message });
        }
    }

    [HttpPost("api/marketplace/webhook")]
    public async Task<IActionResult> Webhook([FromBody] MarketplaceWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Received marketplace webhook: {Action}", payload.Action);

            switch (payload.Action)
            {
                case "Subscribe":
                    // Handle new subscription
                    await HandleSubscribeAsync(payload);
                    break;

                case "Unsubscribe":
                    // Handle cancellation
                    await HandleUnsubscribeAsync(payload);
                    break;

                case "ChangePlan":
                    // Handle plan change
                    await HandleChangePlanAsync(payload);
                    break;

                case "ChangeQuantity":
                    // Handle quantity change
                    await HandleChangeQuantityAsync(payload);
                    break;

                case "Suspend":
                    // Handle suspension (e.g., payment failure)
                    await HandleSuspendAsync(payload);
                    break;

                case "Reinstate":
                    // Handle reinstatement
                    await HandleReinstateAsync(payload);
                    break;

                default:
                    _logger.LogWarning("Unknown marketplace action: {Action}", payload.Action);
                    break;
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing marketplace webhook");
            return StatusCode(500, "Internal server error");
        }
    }

    private async Task<MarketplaceSubscription> ResolveMarketplaceTokenAsync(string token)
    {
        // TODO: Implement actual Marketplace API call
        // This is a simplified example
        // See: https://docs.microsoft.com/en-us/azure/marketplace/partner-center-portal/pc-saas-fulfillment-api-v2
        
        // Use HttpClient to call Marketplace API
        // POST https://marketplaceapi.microsoft.com/api/saas/subscriptions/resolve?api-version=2018-08-31
        // Headers: x-ms-marketplace-token: {token}
        
        await Task.CompletedTask;
        return new MarketplaceSubscription { Id = "sub-" + Guid.NewGuid().ToString() };
    }

    private async Task ActivateSubscriptionAsync(string subscriptionId)
    {
        // TODO: Implement actual Marketplace API call
        // POST https://marketplaceapi.microsoft.com/api/saas/subscriptions/{subscriptionId}/activate?api-version=2018-08-31
        await Task.CompletedTask;
    }

    private async Task HandleSubscribeAsync(MarketplaceWebhookPayload payload)
    {
        // Handle new subscription
        await Task.CompletedTask;
    }

    private async Task HandleUnsubscribeAsync(MarketplaceWebhookPayload payload)
    {
        // Remove purchase status
        await Task.CompletedTask;
    }

    private async Task HandleChangePlanAsync(MarketplaceWebhookPayload payload)
    {
        // Update plan
        await Task.CompletedTask;
    }

    private async Task HandleChangeQuantityAsync(MarketplaceWebhookPayload payload)
    {
        // Update quantity
        await Task.CompletedTask;
    }

    private async Task HandleSuspendAsync(MarketplaceWebhookPayload payload)
    {
        // Suspend access
        await Task.CompletedTask;
    }

    private async Task HandleReinstateAsync(MarketplaceWebhookPayload payload)
    {
        // Reinstate access
        await Task.CompletedTask;
    }
}

public class MarketplaceSubscription
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
}

public class MarketplaceWebhookPayload
{
    public string Action { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public int? Quantity { get; set; }
}
```

## Step 5: Register Webhook

In Program.cs, ensure the webhook endpoint is accessible:

```csharp
app.MapControllers(); // Already added in our implementation
```

## Step 6: Implement Marketplace API Client

Create `Services/MarketplaceApiService.cs`:

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BacpacCompatFixer.Blazor.Services;

public interface IMarketplaceApiService
{
    Task<MarketplaceSubscription> ResolveTokenAsync(string token);
    Task ActivateSubscriptionAsync(string subscriptionId, string planId);
    Task<MarketplaceSubscription> GetSubscriptionAsync(string subscriptionId);
}

public class MarketplaceApiService : IMarketplaceApiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MarketplaceApiService> _logger;
    private const string MarketplaceApiBaseUrl = "https://marketplaceapi.microsoft.com/api/saas";

    public MarketplaceApiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MarketplaceApiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<MarketplaceSubscription> ResolveTokenAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, 
            $"{MarketplaceApiBaseUrl}/subscriptions/resolve?api-version=2018-08-31");
        
        request.Headers.Add("x-ms-marketplace-token", token);
        await AddAuthenticationHeaderAsync(request);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MarketplaceSubscription>(content)
            ?? throw new InvalidOperationException("Failed to deserialize subscription");
    }

    public async Task ActivateSubscriptionAsync(string subscriptionId, string planId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{MarketplaceApiBaseUrl}/subscriptions/{subscriptionId}/activate?api-version=2018-08-31");

        await AddAuthenticationHeaderAsync(request);

        var payload = new { planId };
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<MarketplaceSubscription> GetSubscriptionAsync(string subscriptionId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{MarketplaceApiBaseUrl}/subscriptions/{subscriptionId}?api-version=2018-08-31");

        await AddAuthenticationHeaderAsync(request);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MarketplaceSubscription>(content)
            ?? throw new InvalidOperationException("Failed to deserialize subscription");
    }

    private async Task AddAuthenticationHeaderAsync(HttpRequestMessage request)
    {
        // Get Azure AD token for Marketplace API
        // The Marketplace API requires authentication with Azure AD
        var token = await GetAzureAdTokenAsync();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> GetAzureAdTokenAsync()
    {
        // TODO: Implement token acquisition for Marketplace API
        // Use Azure.Identity library to get token
        await Task.CompletedTask;
        return "your-token-here";
    }
}
```

## Step 7: Update PurchaseVerificationService

Modify the service to integrate with Marketplace API:

```csharp
public async Task<UserPurchaseStatus> VerifyPurchaseAsync(string userId)
{
    // First check local cache
    var localStatus = await GetLocalPurchaseStatusAsync(userId);
    
    if (localStatus?.HasPurchased == true && localStatus.TransactionId != null)
    {
        // Verify with Marketplace API
        try
        {
            var subscription = await _marketplaceApi.GetSubscriptionAsync(localStatus.TransactionId);
            if (subscription.Status == "Active")
            {
                return localStatus;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify marketplace subscription for user {UserId}", userId);
        }
    }

    // Return free tier if no valid purchase found
    return new UserPurchaseStatus
    {
        UserId = userId,
        HasPurchased = false,
        MaxFileSizeBytes = FreeTierMaxFileSize
    };
}
```

## Step 8: Testing

### Test in Preview Mode

1. Submit your offer for preview
2. Microsoft will review (1-3 business days)
3. Once approved, you can test with your Azure subscription
4. Use the preview link provided by Partner Center

### Test Purchase Flow

1. Navigate to preview link
2. Click "Get It Now"
3. Sign in with test account
4. Complete purchase flow
5. Verify landing page receives token
6. Verify subscription is activated
7. Verify user gets premium access

## Step 9: Go Live

1. Complete all certification requirements
2. Submit for final review
3. Once approved, your offer will be live
4. Monitor Partner Center dashboard for purchases

## Webhook Security

Verify webhook authenticity:

```csharp
[HttpPost("api/marketplace/webhook")]
public async Task<IActionResult> Webhook(
    [FromBody] MarketplaceWebhookPayload payload,
    [FromHeader(Name = "x-ms-signature")] string signature)
{
    // Verify signature
    if (!VerifyWebhookSignature(Request.Body, signature))
    {
        _logger.LogWarning("Invalid webhook signature");
        return Unauthorized();
    }

    // Process webhook...
}

private bool VerifyWebhookSignature(Stream body, string signature)
{
    // TODO: Implement signature verification
    // Use your webhook secret to verify HMAC-SHA256 signature
    return true;
}
```

## Monitoring

Monitor these metrics:
- Subscription activations
- Failed webhook deliveries
- API call failures
- User conversion rate

## Resources

- [Microsoft Commercial Marketplace Documentation](https://docs.microsoft.com/en-us/azure/marketplace/)
- [SaaS Fulfillment APIs v2](https://docs.microsoft.com/en-us/azure/marketplace/partner-center-portal/pc-saas-fulfillment-api-v2)
- [Partner Center Portal](https://partner.microsoft.com/dashboard)
- [Marketplace Metering Service API](https://docs.microsoft.com/en-us/azure/marketplace/partner-center-portal/marketplace-metering-service-apis)

## Support

For marketplace integration issues:
- Partner Center Support
- [Azure Marketplace Forum](https://docs.microsoft.com/en-us/answers/topics/azure-marketplace.html)
- Microsoft Partner Support
