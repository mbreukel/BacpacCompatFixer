# Premium Plan Management - Quick Start Guide

## ?? Was wurde implementiert?

Ein vollautomatisches System, das Benutzer basierend auf ihrem Microsoft Marketplace-Plan automatisch auf Premium hochstuft oder herabstuft.

## ?? Konfiguration

### 1. Premium-Pläne definieren

Öffne `appsettings.json` und füge deine Premium-Plan-IDs hinzu:

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

**Wichtig**: Diese Plan-IDs müssen mit den Plan-IDs in deinem Microsoft Partner Center Angebot übereinstimmen!

### 2. Wo finde ich die Plan-IDs?

1. Gehe zu [Microsoft Partner Center](https://partner.microsoft.com/)
2. Navigiere zu: **Marketplace Angebote** ? **Dein SaaS-Angebot** ? **Plan overview**
3. Kopiere die **Plan ID** für jeden Plan, der Premium-Zugang gewähren soll
4. Füge diese IDs zum `PremiumPlanIds` Array hinzu

## ?? Wie es funktioniert

### Automatische Upgrades/Downgrades

Wenn ein Benutzer einen Plan abschließt:

1. **Microsoft sendet Webhook** ? `POST /api/MarketplaceWebhook`
2. **System prüft Plan-ID** ? Ist sie in `PremiumPlanIds`?
3. **Automatisches Upgrade**:
   - ? `HasPurchased = true`
   - ? `Status = Active`
   - ? `MaxFileSizeBytes = 5GB` (statt 500MB)
4. **Benutzer hat sofort Premium-Zugang**

### Unterstützte Aktionen

| Webhook-Aktion | Was passiert |
|---------------|--------------|
| `ChangePlan` | Prüft neuen Plan ? Upgrade/Downgrade |
| `Unsubscribe` | Entfernt Premium-Zugang ? Free Tier |
| `Suspend` | Sperrt Premium temporär |
| `Reinstate` | Stellt Premium wieder her |
| `Renew` | Bestätigt Premium-Status |

## ?? Verwendung im Code

### Beispiel 1: Premium-Zugang prüfen

```csharp
public class MyService
{
    private readonly IPurchaseVerificationService _purchaseService;

    public async Task<bool> CanAccessPremiumFeature(string userId)
    {
        var status = await _purchaseService.VerifyPurchaseAsync(userId);
        return status.HasPurchased && status.Status == SubscriptionStatus.Active;
    }
}
```

### Beispiel 2: Datei-Upload mit Größenprüfung

```csharp
[HttpPost("upload")]
public async Task<IActionResult> UploadFile(IFormFile file)
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var status = await _purchaseService.VerifyPurchaseAsync(userId);

    if (file.Length > status.MaxFileSizeBytes)
    {
        var maxSizeMB = status.MaxFileSizeBytes / (1024 * 1024);
        return BadRequest($"File too large. Your limit: {maxSizeMB}MB");
    }

    // Verarbeite Datei...
    return Ok();
}
```

### Beispiel 3: Feature-Gating

```csharp
public async Task<IActionResult> ProcessBatch()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var status = await _purchaseService.VerifyPurchaseAsync(userId);

    if (!status.HasPurchased)
    {
        return Unauthorized("This feature requires a Premium subscription");
    }

    // Premium-Feature ausführen...
    return Ok();
}
```

## ?? Testen

### Manuell Premium-Zugang gewähren (Entwicklung)

```csharp
// Inject den Service
private readonly IPurchaseVerificationService _purchaseService;

// Gewähre Premium für Tests
await _purchaseService.UpdateSubscriptionAsync(
    subscriptionId: "test-sub-123",
    planId: "test-premium", // Muss in PremiumPlanIds sein!
    status: SubscriptionStatus.Active,
    userEmail: "test@example.com"
);

// Prüfe Status
var status = await _purchaseService.VerifyPurchaseAsync("test@example.com");
Console.WriteLine($"Has Premium: {status.HasPurchased}"); // True
Console.WriteLine($"Max File Size: {status.MaxFileSizeBytes / (1024*1024)}MB"); // 5120MB
```

### Health Check

```bash
curl https://localhost:5001/api/MarketplaceWebhook/health
```

Antwort:
```json
{
  "status": "healthy",
  "timestamp": "2025-01-15T10:30:00Z"
}
```

## ?? Status-Übersicht

| Status | Beschreibung | Premium-Zugang | Max. Dateigröße |
|--------|--------------|----------------|-----------------|
| `Free` | Kein Abo | ? | 500 MB |
| `Active` | Premium aktiv | ? | 5 GB |
| `Suspended` | Zahlungsproblem | ? | 500 MB |
| `Unsubscribed` | Gekündigt | ? | 500 MB |

## ?? Erweiterte Konfiguration

### Produktion: Datenbank statt Dateien

Die aktuelle Implementierung nutzt Dateien (`App_Data/Purchases/`). Für Produktion empfohlen:

```csharp
// 1. Erstelle Entity
public class UserSubscription
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public string SubscriptionId { get; set; }
    public string PlanId { get; set; }
    public SubscriptionStatus Status { get; set; }
    // ...
}

// 2. Füge zu DbContext hinzu
public class ApplicationDbContext : DbContext
{
    public DbSet<UserSubscription> UserSubscriptions { get; set; }
}

// 3. Implementiere Service mit EF Core
public async Task<UserPurchaseStatus> VerifyPurchaseAsync(string userId)
{
    var subscription = await _context.UserSubscriptions
        .FirstOrDefaultAsync(s => s.UserId == userId);
    // ...
}
```

### Caching hinzufügen

```csharp
// In Program.cs
builder.Services.AddMemoryCache();

// In Service
private readonly IMemoryCache _cache;

public async Task<UserPurchaseStatus> VerifyPurchaseAsync(string userId)
{
    return await _cache.GetOrCreateAsync($"purchase_{userId}", async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        return await LoadFromStorage(userId);
    });
}
```

## ?? Dateistruktur

```
App_Data/
??? Purchases/           # Benutzer-Abonnements
?   ??? user1@example.com.json
?   ??? user2@example.com.json
??? Subscriptions/       # Subscription-ID Index
    ??? sub-abc-123.txt  ? user1@example.com
    ??? sub-def-456.txt  ? user2@example.com
```

## ?? Troubleshooting

### Problem: Benutzer bekommt kein Premium

**Lösung**:
1. Prüfe Plan-ID in Logs:
   ```
   [Information] Handling plan change for subscription sub-123 to plan standard
   [Information] User plan updated to standard (Premium: False)
   ```
2. Füge "standard" zu `PremiumPlanIds` hinzu
3. Webhook wird bei nächster Änderung ausgelöst (oder manuell testen)

### Problem: Status bleibt auf "Free"

**Lösung**:
1. Prüfe `App_Data/Purchases/{userId}.json`
2. Sollte enthalten:
   ```json
   {
     "Status": "Active",
     "HasPurchased": true,
     "PlanId": "premium"
   }
   ```
3. Falls nicht: Webhook-Empfang prüfen (siehe Logs)

### Problem: 401 Unauthorized beim Webhook

**Lösung**:
1. Prüfe `appsettings.json`:
   ```json
   {
     "AzureAd": {
       "ClientId": "your-actual-client-id"
     }
   }
   ```
2. ClientId muss mit Partner Center übereinstimmen

## ?? Weitere Dokumentation

- **Vollständige Dokumentation**: `MARKETPLACE_WEBHOOK_README.md`
- **Code-Beispiele**: `Examples/PremiumAccessExamples.cs`
- **Microsoft Docs**: [SaaS fulfillment APIs](https://docs.microsoft.com/en-us/azure/marketplace/partner-center-portal/pc-saas-fulfillment-api-v2)

## ? Checkliste für Partner Center

- [ ] Webhook-URL konfiguriert: `https://your-domain.com/api/MarketplaceWebhook`
- [ ] Azure AD Client ID eingetragen
- [ ] Tenant ID eingetragen
- [ ] Plan-IDs notiert und in `appsettings.json` eingetragen
- [ ] Testsubskription durchgeführt
- [ ] Webhook-Logs geprüft

## ?? Fertig!

Das System ist jetzt bereit:
- ? Automatische Premium-Aktivierung bei Kauf
- ? Automatische Downgrades bei Kündigung
- ? Suspension-Handling bei Zahlungsproblemen
- ? Plan-Wechsel-Unterstützung
- ? Einfache Integration in deinen Code

**Nächste Schritte**:
1. Plan-IDs in `appsettings.json` eintragen
2. Webhook-URL im Partner Center konfigurieren
3. Testsubskription durchführen
4. Premium-Features in deiner App verwenden!
