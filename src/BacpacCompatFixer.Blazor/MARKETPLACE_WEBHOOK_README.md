# Microsoft Marketplace SaaS Webhook Implementation

## Overview
This implementation provides a webhook endpoint for receiving and processing Microsoft Marketplace SaaS subscription events with automatic premium access management.

## Components

### 1. Models
- **MarketplaceWebhookEvent.cs**: Represents webhook event data from Microsoft Marketplace
- **PurchaserInfo**: Contains information about the purchaser
- **UserPurchaseStatus**: Extended with subscription tracking (SubscriptionId, PlanId, Status)
- **SubscriptionStatus**: Enum for subscription states (Free, Active, Suspended, Unsubscribed)

### 2. Services
- **IJwtTokenValidationService/JwtTokenValidationService**: Validates JWT tokens from Microsoft Marketplace webhook requests
- **IPurchaseVerificationService/PurchaseVerificationService**: Manages user subscriptions and premium access

### 3. Controllers
- **MarketplaceWebhookController**: Handles incoming webhook events and automatically grants/revokes premium access

## Premium Plan Configuration

### appsettings.json
```json
{
  "Marketplace": {
    "PremiumPlanIds": [
      "premium",
      "pro",
      "professional",
      "enterprise"
    ]
  }
}
```

**How it works:**
- When a webhook event is received, the system checks if the `PlanId` matches any of the configured premium plan IDs
- Users with a premium plan get:
  - `HasPurchased = true`
  - `MaxFileSizeBytes = 5GB` (vs 500MB for free)
  - `Status = Active`
- Plan IDs are case-insensitive

### Adding Your Own Plan IDs

1. Go to Microsoft Partner Center ? Your Offer ? Plan overview
2. Copy the Plan ID (e.g., "standard", "gold", "yearly")
3. Add it to the `PremiumPlanIds` array in appsettings.json

Example:
```json
{
  "Marketplace": {
    "PremiumPlanIds": [
      "standard",
      "gold",
      "yearly"
    ]
  }
}
```

## Supported Webhook Actions

The webhook controller automatically manages premium access based on these events:

### 1. **Unsubscribe** 
User cancels subscription
- **Action**: Revokes premium access
- **Status**: `Unsubscribed`
- **Effect**: User drops back to free tier

### 2. **ChangePlan/ChangeMessage**
User changes subscription plan
- **Action**: Updates plan and checks if new plan is premium
- **Status**: `Active` (if premium) or `Free` (if downgraded)
- **Effect**: Access level adjusts automatically

### 3. **Suspend**
Subscription is suspended (payment issues, etc.)
- **Action**: Temporarily suspends premium access
- **Status**: `Suspended`
- **Effect**: User loses premium features until reinstated

### 4. **Reinstate**
Suspended subscription is reinstated
- **Action**: Restores premium access if plan is premium
- **Status**: `Active` (if premium) or `Free`
- **Effect**: Premium features restored

### 5. **Renew**
Subscription is renewed
- **Action**: Confirms premium status
- **Status**: `Active` (if premium)
- **Effect**: Premium access continues

## Data Storage

The service uses file-based storage with two directories:

### User Data (`App_Data/Purchases/`)
Stores user purchase status by UserId:
```json
{
  "UserId": "user@example.com",
  "Email": "user@example.com",
  "HasPurchased": true,
  "PurchaseDate": "2025-01-15T10:30:00Z",
  "MaxFileSizeBytes": 5368709120,
  "TransactionId": "trans-123",
  "SubscriptionId": "sub-abc-123",
  "PlanId": "premium",
  "Status": "Active",
  "LastUpdated": "2025-01-15T10:30:00Z"
}
```

### Subscription Index (`App_Data/Subscriptions/`)
Maps SubscriptionId ? UserId for fast lookups

## Configuration

### appsettings.json (Complete)
```json
{
  "AzureAd": {
    "ClientId": "your-client-id-here"
  },
  "Marketplace": {
    "PremiumPlanIds": [
      "premium",
      "pro",
      "professional",
      "enterprise"
    ]
  },
  "MarketplaceWebhook": {
    "EndpointUrl": "/api/marketplacewebhook",
    "EnableTokenValidation": true
  }
}
```

### Required Settings
- **AzureAd:ClientId**: Your Azure AD application client ID
- **Marketplace:PremiumPlanIds**: Array of plan IDs that grant premium access

## Webhook Endpoint

### URL
```
POST https://your-app-domain.com/api/MarketplaceWebhook
```

### Authentication
The endpoint expects a JWT token in the Authorization header:
```
Authorization: Bearer <jwt-token>
```

### Request Body Example
```json
{
  "id": "unique-event-id",
  "activityId": "activity-id",
  "subscriptionId": "sub-abc-123",
  "offerId": "offer-id",
  "planId": "premium",
  "publisherId": "publisher-id",
  "action": "ChangePlan",
  "timeStamp": "2025-01-15T10:30:00Z",
  "status": "Succeeded",
  "purchaser": {
    "emailId": "user@example.com",
    "objectId": "azure-ad-object-id",
    "tenantId": "tenant-id",
    "puid": "puid"
  }
}
```

## Usage Example

### Checking User Access in Your Code

```csharp
public class MyService
{
    private readonly IPurchaseVerificationService _purchaseService;

    public async Task<bool> CanUserAccessPremiumFeature(string userId)
    {
        var status = await _purchaseService.VerifyPurchaseAsync(userId);
        
        // Check if user has active premium subscription
        return status.HasPurchased && status.Status == SubscriptionStatus.Active;
    }

    public async Task<long> GetUserMaxFileSize(string userId)
    {
        var status = await _purchaseService.VerifyPurchaseAsync(userId);
        return status.MaxFileSizeBytes; // 5GB for premium, 500MB for free
    }
}
```

## Testing

### Health Check Endpoint
```
GET https://your-app-domain.com/api/MarketplaceWebhook/health
```

Response:
```json
{
  "status": "healthy",
  "timestamp": "2025-01-15T10:30:00Z"
}
```

### Manual Testing (Development)

You can manually test the premium logic:

```csharp
// Simulate a premium subscription
await _purchaseService.UpdateSubscriptionAsync(
    subscriptionId: "test-sub-123",
    planId: "premium", // Must match PremiumPlanIds
    status: SubscriptionStatus.Active,
    userEmail: "test@example.com"
);

// Verify the user now has premium access
var status = await _purchaseService.VerifyPurchaseAsync("test@example.com");
Assert.True(status.HasPurchased);
Assert.Equal(5GB, status.MaxFileSizeBytes);
```

## Setting up in Microsoft Partner Center

1. Go to Partner Center ? Your SaaS Offer ? Technical Configuration
2. Set the **Connection webhook** to: `https://your-app-domain.com/api/MarketplaceWebhook`
3. Set the **Azure Active Directory tenant ID**: Your tenant ID
4. Set the **Azure Active Directory application ID**: Your Azure AD app client ID
5. Under **Plans**, note the Plan ID for each plan you want to grant premium access

## Premium Access Flow

```
User Purchases ? Webhook Event ? Controller ? PurchaseService
                                              ?
                                    Check PlanId in PremiumPlanIds
                                              ?
                          Yes ? Is Premium? ? No
                           ?                   ?
                    Status = Active     Status = Free
                    5GB File Limit      500MB File Limit
                    HasPurchased = true HasPurchased = false
```

## Implementation Details

### Automatic Premium Detection
The service automatically grants premium access when:
- Webhook action is `ChangePlan` or `Reinstate` or `Renew`
- AND `PlanId` matches any entry in `Marketplace:PremiumPlanIds` (case-insensitive)

### File Size Limits
- **Free Tier**: 500 MB (524,288,000 bytes)
- **Premium Tier**: 5 GB (5,368,709,120 bytes)

### Subscription Status Management
- **Free**: No active subscription
- **Active**: Premium subscription active and paid
- **Suspended**: Payment issue, temporary loss of access
- **Unsubscribed**: User cancelled, permanent loss until resubscribe

## Monitoring & Logging

All webhook events are logged at Information level:
```
[Information] Received webhook event: ChangePlan for subscription sub-abc-123
[Information] User plan updated to premium (Premium: True) for subscription sub-abc-123
```

Enable detailed logging in appsettings.json:
```json
{
  "Logging": {
    "LogLevel": {
      "BacpacCompatFixer.Blazor.Controllers.MarketplaceWebhookController": "Debug",
      "BacpacCompatFixer.Blazor.Services.PurchaseVerificationService": "Debug"
    }
  }
}
```

## Security Considerations

- ? JWT token validation is enforced
- ? Tokens are validated against Microsoft signing keys
- ? HTTPS is required (enforced in Program.cs)
- ? Subscription status is tracked and validated
- ?? Consider implementing rate limiting for webhook endpoint
- ?? Consider adding IP whitelist for Microsoft Marketplace IPs
- ?? In production, replace file-based storage with a database

## Production Recommendations

### Replace File-Based Storage
For production, implement a database-backed storage:

```csharp
// Example with Entity Framework
public class UserSubscription
{
    public string UserId { get; set; }
    public string SubscriptionId { get; set; }
    public string PlanId { get; set; }
    public SubscriptionStatus Status { get; set; }
    // ... other fields
}

// Use DbContext instead of file storage
public class PurchaseVerificationService
{
    private readonly ApplicationDbContext _context;
    
    public async Task<UserPurchaseStatus> VerifyPurchaseAsync(string userId)
    {
        var subscription = await _context.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);
        // ... convert to UserPurchaseStatus
    }
}
```

### Add Caching
Cache user subscription status to reduce file/database lookups:

```csharp
services.AddMemoryCache();
services.AddScoped<ICachedPurchaseService, CachedPurchaseService>();
```

## Troubleshooting

### User Not Getting Premium Access

1. **Check Plan ID Configuration**
   ```bash
   # Verify your plan ID is in the config
   cat appsettings.json | grep -A 5 "PremiumPlanIds"
   ```

2. **Check Webhook Logs**
   ```
   [Information] Handling plan change for subscription sub-123 to plan premium
   [Information] User plan updated to premium (Premium: True) for subscription sub-123
   ```
   If you see `(Premium: False)`, the plan ID doesn't match

3. **Verify Subscription File**
   Check `App_Data/Purchases/{userId}.json`:
   ```json
   {
     "PlanId": "premium",
     "Status": "Active",
     "HasPurchased": true
   }
   ```

### Common Issues

1. **401 Unauthorized**
   - Check that AzureAd:ClientId matches the audience in the JWT token

2. **Premium Not Activated**
   - Verify plan ID is in `Marketplace:PremiumPlanIds` array
   - Check case-sensitivity (should be case-insensitive, but verify)
   - Look for typos in plan ID

3. **Webhook Not Receiving Events**
   - Verify webhook URL in Partner Center
   - Check application logs for incoming requests

## Dependencies

- Microsoft.Identity.Web (4.1.0)
- Microsoft.Identity.Web.UI (4.1.0)
- Microsoft.IdentityModel.Tokens (8.15.0)
- System.IdentityModel.Tokens.Jwt (8.15.0)
- Microsoft.AspNetCore.Authentication.OpenIdConnect (10.0.0)

## Summary

? **Automatic Premium Management**: Users automatically get premium access when they purchase a premium plan  
? **Configurable Plans**: Easy to add/remove premium plans via configuration  
? **Full Lifecycle Support**: Handles subscription, plan changes, suspension, and cancellation  
? **Production Ready**: Includes logging, error handling, and security validation  
? **Easy Integration**: Simple API to check user access in your code
