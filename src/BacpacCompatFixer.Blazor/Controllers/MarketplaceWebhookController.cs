using BacpacCompatFixer.Blazor.Models;
using BacpacCompatFixer.Blazor.Services;
using Microsoft.AspNetCore.Mvc;

namespace BacpacCompatFixer.Blazor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketplaceWebhookController : ControllerBase
{
    private readonly IJwtTokenValidationService _jwtValidationService;
    private readonly IPurchaseVerificationService _purchaseService;
    private readonly ILogger<MarketplaceWebhookController> _logger;

    public MarketplaceWebhookController(
        IJwtTokenValidationService jwtValidationService,
        IPurchaseVerificationService purchaseService,
        ILogger<MarketplaceWebhookController> logger)
    {
        _jwtValidationService = jwtValidationService;
        _purchaseService = purchaseService;
        _logger = logger;
    }

    /// <summary>
    /// Receives webhook events from Microsoft Marketplace
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook([FromBody] MarketplaceWebhookEvent webhookEvent)
    {
        try
        {
            _logger.LogInformation("Received webhook event: {Action} for subscription {SubscriptionId}", 
                webhookEvent.Action, webhookEvent.SubscriptionId);

            // Validate JWT token from Authorization header
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                _logger.LogWarning("No Authorization header found");
                return Unauthorized(new { error = "Missing authorization header" });
            }

            var token = authHeader.ToString().Replace("Bearer ", "");
            var isValid = await _jwtValidationService.ValidateTokenAsync(token);
            
            if (!isValid)
            {
                _logger.LogWarning("Invalid JWT token");
                return Unauthorized(new { error = "Invalid token" });
            }

            // Process the webhook event based on action
            await ProcessWebhookEventAsync(webhookEvent);

            return Ok(new { message = "Webhook processed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook event");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private async Task ProcessWebhookEventAsync(MarketplaceWebhookEvent webhookEvent)
    {
        _logger.LogInformation("Processing webhook action: {Action}", webhookEvent.Action);

        switch (webhookEvent.Action.ToLower())
        {
            case "unsubscribe":
                await HandleUnsubscribeAsync(webhookEvent);
                break;

            case "changemessage":
            case "changeplan":
                await HandlePlanChangeAsync(webhookEvent);
                break;

            case "suspend":
                await HandleSuspendAsync(webhookEvent);
                break;

            case "reinstate":
                await HandleReinstateAsync(webhookEvent);
                break;

            case "renew":
                await HandleRenewAsync(webhookEvent);
                break;

            default:
                _logger.LogWarning("Unknown webhook action: {Action}", webhookEvent.Action);
                break;
        }
    }

    private async Task HandleUnsubscribeAsync(MarketplaceWebhookEvent webhookEvent)
    {
        _logger.LogInformation("Handling unsubscribe for subscription {SubscriptionId}", 
            webhookEvent.SubscriptionId);
        
        await _purchaseService.UpdateSubscriptionAsync(
            webhookEvent.SubscriptionId,
            webhookEvent.PlanId,
            SubscriptionStatus.Unsubscribed,
            webhookEvent.Purchaser?.EmailId
        );

        _logger.LogInformation("User access revoked for subscription {SubscriptionId}", 
            webhookEvent.SubscriptionId);
    }

    private async Task HandlePlanChangeAsync(MarketplaceWebhookEvent webhookEvent)
    {
        _logger.LogInformation("Handling plan change for subscription {SubscriptionId} to plan {PlanId}", 
            webhookEvent.SubscriptionId, webhookEvent.PlanId);
        
        // Determine if the new plan is premium
        var isPremium = _purchaseService.IsPremiumPlan(webhookEvent.PlanId);
        var newStatus = isPremium ? SubscriptionStatus.Active : SubscriptionStatus.Free;

        await _purchaseService.UpdateSubscriptionAsync(
            webhookEvent.SubscriptionId,
            webhookEvent.PlanId,
            newStatus,
            webhookEvent.Purchaser?.EmailId
        );

        _logger.LogInformation("User plan updated to {PlanId} (Premium: {IsPremium}) for subscription {SubscriptionId}", 
            webhookEvent.PlanId, isPremium, webhookEvent.SubscriptionId);
    }

    private async Task HandleSuspendAsync(MarketplaceWebhookEvent webhookEvent)
    {
        _logger.LogInformation("Handling suspend for subscription {SubscriptionId}", 
            webhookEvent.SubscriptionId);
        
        await _purchaseService.UpdateSubscriptionAsync(
            webhookEvent.SubscriptionId,
            webhookEvent.PlanId,
            SubscriptionStatus.Suspended,
            webhookEvent.Purchaser?.EmailId
        );

        _logger.LogInformation("User access suspended for subscription {SubscriptionId}", 
            webhookEvent.SubscriptionId);
    }

    private async Task HandleReinstateAsync(MarketplaceWebhookEvent webhookEvent)
    {
        _logger.LogInformation("Handling reinstate for subscription {SubscriptionId}", 
            webhookEvent.SubscriptionId);
        
        // Reinstate to active if plan is premium
        var isPremium = _purchaseService.IsPremiumPlan(webhookEvent.PlanId);
        var newStatus = isPremium ? SubscriptionStatus.Active : SubscriptionStatus.Free;

        await _purchaseService.UpdateSubscriptionAsync(
            webhookEvent.SubscriptionId,
            webhookEvent.PlanId,
            newStatus,
            webhookEvent.Purchaser?.EmailId
        );

        _logger.LogInformation("User access reinstated for subscription {SubscriptionId} with status {Status}", 
            webhookEvent.SubscriptionId, newStatus);
    }

    private async Task HandleRenewAsync(MarketplaceWebhookEvent webhookEvent)
    {
        _logger.LogInformation("Handling renew for subscription {SubscriptionId}", 
            webhookEvent.SubscriptionId);
        
        // Ensure subscription remains active
        var isPremium = _purchaseService.IsPremiumPlan(webhookEvent.PlanId);
        var status = isPremium ? SubscriptionStatus.Active : SubscriptionStatus.Free;

        await _purchaseService.UpdateSubscriptionAsync(
            webhookEvent.SubscriptionId,
            webhookEvent.PlanId,
            status,
            webhookEvent.Purchaser?.EmailId
        );

        _logger.LogInformation("Subscription renewed for {SubscriptionId}", 
            webhookEvent.SubscriptionId);
    }

    /// <summary>
    /// Health check endpoint for webhook
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
