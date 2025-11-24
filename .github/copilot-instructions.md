# GitHub Copilot Instructions - BacpacCompatFixer Repository

## ?? Repository-Übersicht

**BacpacCompatFixer** ist eine .NET 10 Anwendung zur Konvertierung von SQL Server BACPAC-Dateien mit Microsoft Marketplace SaaS-Integration.

### Projekte:
- **BacpacCompatFixer.Core** - Kern-Logik für BACPAC-Verarbeitung
- **BacpacCompatFixer.Console** - CLI-Anwendung
- **BacpacCompatFixer.Blazor** - Web-Anwendung mit Marketplace-Integration

---

## ?? WICHTIG: Secrets Management

### Wo werden Secrets gespeichert?

**Dieses Repository verwendet:**
- ? **Azure App Service Environment Variables** (Production)
- ? **User Secrets** (`dotnet user-secrets`) für lokale Entwicklung
- ? **NIEMALS in appsettings.json oder Code**

### Konfiguration:

#### Lokale Entwicklung:
```bash
cd src/BacpacCompatFixer.Blazor
dotnet user-secrets set "AzureAd:ClientSecret" "your-secret-here"
```

#### Azure App Service (Production):
```
Environment Variable Name: AzureAd__ClientSecret
(Doppelter Unterstrich für nested configuration!)
```

### appsettings.json Struktur:
```json
{
  "AzureAd": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id"
    // ClientSecret kommt aus Environment Variables!
  }
}
```

**Siehe auch:** `src/BacpacCompatFixer.Blazor/SECRETS_MANAGEMENT.md`

---

## ??? Architektur: Microsoft Marketplace Integration

### API-basierte Echtzeit-Verifizierung (seit v2.0)

Die Anwendung verwendet **KEINE lokale Datenspeicherung** für Subscriptions, sondern fragt bei jedem Login die **Microsoft Marketplace Fulfillment API** ab.

### Services:

1. **MarketplaceAuthService** (`Services/MarketplaceAuthService.cs`)
   - Authentifizierung mit Azure AD
   - Holt Access Token für Marketplace API
   - Token-Caching (~55 Minuten)

2. **MarketplaceApiService** (`Services/MarketplaceApiService.cs`)
   - Implementiert Marketplace Fulfillment API v2
   - `GetAllSubscriptionsAsync()` - Alle Subscriptions
   - `GetSubscriptionByIdAsync(id)` - Subscription per ID
   - `GetSubscriptionByUserEmailAsync(email)` - Subscription per E-Mail

3. **RealTimePurchaseVerificationService** (`Services/RealTimePurchaseVerificationService.cs`)
   - Ersetzt alte dateibasierte Lösung
   - Prüft bei jedem Login via API
   - Optional: 5-Minuten-Cache (konfigurierbar)
   - Webhook-Events invalidieren Cache

### Flow:
```
User Login ? Check Cache (5 Min) ? Cache Miss
              ?
    Query Microsoft Marketplace API
              ?
    GET /api/saas/subscriptions
              ?
    Find user's subscription
              ?
    PlanId in PremiumPlanIds? ? Premium (5GB) : Free (500MB)
```

### Webhook-Integration:

**Endpoint:** `/api/MarketplaceWebhook`

**Events:**
- `ChangePlan` - User ändert Plan
- `Unsubscribe` - User kündigt
- `Suspend` - Zahlung fehlgeschlagen
- `Reinstate` - Zahlung wiederhergestellt
- `Renew` - Subscription erneuert

**Webhook invalidiert Cache automatisch!**

---

## ?? Premium-Plan-Management

### Konfiguration in appsettings.json:

```json
{
  "Marketplace": {
    "PremiumPlanIds": [
      "premium",
      "pro",
      "enterprise"
    ],
    "EnableCaching": true,
    "CacheDurationMinutes": 5
  }
}
```

### Plan-IDs aus Partner Center:
1. Partner Center ? Marketplace offers ? Ihr Angebot
2. Plan overview ? Kopiere "Plan ID"
3. Füge zu `PremiumPlanIds` Array hinzu

### Premium-Logik:
```csharp
var status = await _purchaseService.VerifyPurchaseAsync(userEmail);

if (status.HasPurchased && status.Status == SubscriptionStatus.Active)
{
    // Premium User - 5GB Limit
}
else
{
    // Free User - 500MB Limit
}
```

---

## ?? Entwicklungs-Richtlinien

### Code-Style:
- ? C# 12 Features (`.NET 10`)
- ? Nullable Reference Types aktiviert
- ? Async/Await für alle I/O-Operationen
- ? Dependency Injection über Constructor
- ? Strukturiertes Logging mit `ILogger<T>`

### Naming Conventions:
- Interfaces: `I{Name}Service` (z.B. `IMarketplaceApiService`)
- Async-Methoden: `{Verb}Async` (z.B. `GetSubscriptionAsync`)
- Private Fields: `_camelCase` (z.B. `_httpClient`)
- Constants: `PascalCase` (z.B. `FreeTierMaxFileSize`)

### Dependency Injection:
```csharp
// In Program.cs registrieren
builder.Services.AddScoped<IMarketplaceAuthService, MarketplaceAuthService>();
builder.Services.AddScoped<IMarketplaceApiService, MarketplaceApiService>();
builder.Services.AddScoped<IPurchaseVerificationService, RealTimePurchaseVerificationService>();
```

### Logging:
```csharp
_logger.LogInformation("User {UserId} has {Status} subscription", userId, status);
_logger.LogWarning("Subscription {SubscriptionId} not found", subscriptionId);
_logger.LogError(ex, "Error processing webhook event");
```

---

## ?? Dokumentation

### Haupt-Dokumentation (in `src/BacpacCompatFixer.Blazor/`):

| Dokument | Zweck | Dauer |
|----------|-------|-------|
| **QUICK_START_API.md** | Schnell-Start Guide | 5 Min |
| **REALTIME_API_VERIFICATION_README.md** | Vollständige Referenz | 20 Min |
| **MIGRATION_GUIDE.md** | Upgrade von v1 zu v2 | 30 Min |
| **SECRETS_MANAGEMENT.md** | Secrets-Konfiguration | 10 Min |
| **MARKETPLACE_WEBHOOK_README.md** | Webhook-Integration | 10 Min |
| **PREMIUM_QUICKSTART.md** | Premium-Features | 10 Min |
| **ARCHITECTURE_DIAGRAM.md** | Architektur-Visualisierung | 5 Min |
| **README_DOCUMENTATION_INDEX.md** | Dokumentations-Index | - |

### Code-Beispiele:
- `Examples/PremiumAccessExamples.cs` - 10+ Verwendungsbeispiele

---

## ?? Was NICHT tun

### Secrets:
- ? NIEMALS Secrets in appsettings.json
- ? NIEMALS Secrets in Code hardcoden
- ? NIEMALS Secrets in Git committen
- ? NIEMALS Secrets in Logs ausgeben

### Lokale Speicherung:
- ? KEINE JSON-Dateien für Subscriptions
- ? KEINE `App_Data/Purchases/` Dateien
- ? Stattdessen: Microsoft Marketplace API abfragen

### API-Calls:
- ? Keine ungecachten API-Calls in Loops
- ? Keine parallelen Calls ohne Rate Limiting
- ? Verwende 5-Minuten-Cache für Performance

---

## ? Best Practices

### Performance:
```csharp
// ? Mit Cache (empfohlen)
"Marketplace": {
  "EnableCaching": true,
  "CacheDurationMinutes": 5
}
// Reduziert API-Calls um 80%!
```

### Error Handling:
```csharp
try
{
    var subscription = await _marketplaceApi.GetSubscriptionAsync(id);
    // ...
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "API call failed");
    // Return fail-safe default (Free Tier)
    return FreeTierStatus;
}
```

### Thread Safety:
```csharp
private readonly SemaphoreSlim _tokenLock = new(1, 1);

await _tokenLock.WaitAsync();
try
{
    // Critical section
}
finally
{
    _tokenLock.Release();
}
```

---

## ?? Testing

### Lokaler Test:
```bash
cd src/BacpacCompatFixer.Blazor
dotnet user-secrets set "AzureAd:ClientSecret" "test-secret"
dotnet run
```

**Erwartete Logs:**
```
[Information] Successfully obtained Marketplace API access token
[Information] Querying Marketplace API for user test@example.com
[Information] User has Active subscription with plan premium (Premium: True)
```

### Build:
```bash
dotnet build
```

**Alle Tests müssen erfolgreich sein!**

---

## ?? Monitoring & Logs

### Wichtige Log-Kategorien:
```json
"Logging": {
  "LogLevel": {
    "BacpacCompatFixer.Blazor.Services.RealTimePurchaseVerificationService": "Information",
    "BacpacCompatFixer.Blazor.Services.MarketplaceApiService": "Information",
    "BacpacCompatFixer.Blazor.Services.MarketplaceAuthService": "Information",
    "BacpacCompatFixer.Blazor.Controllers.MarketplaceWebhookController": "Information"
  }
}
```

### Performance-Metriken:
- Cache Hit: ~1ms
- API Call: ~50-200ms
- Token Refresh: ~100ms (stündlich)

### API-Calls pro Stunde (mit Cache):
- 10 Users: ~120 Calls
- 100 Users: ~1.200 Calls
- 1000 Users: ~12.000 Calls

---

## ?? Deployment

### Azure App Service:

1. **Environment Variables setzen:**
```bash
az webapp config appsettings set \
  --resource-group your-rg \
  --name your-app \
  --settings AzureAd__ClientSecret="your-secret"
```

2. **Deploy:**
```bash
dotnet publish -c Release
# Deploy to Azure App Service
```

3. **Logs prüfen:**
```bash
az webapp log tail --name your-app --resource-group your-rg
```

---

## ?? Troubleshooting

### Problem: "Azure AD configuration is missing"
**Lösung:** Client Secret fehlt
```bash
dotnet user-secrets set "AzureAd:ClientSecret" "your-secret"
```

### Problem: "No active subscription found"
**Lösung:** 
1. Prüfe E-Mail-Adresse
2. Prüfe Partner Center ? Subscriptions
3. Prüfe Logs für API-Response

### Problem: User bekommt kein Premium
**Lösung:**
1. Prüfe `PremiumPlanIds` in appsettings.json
2. Logs zeigen: `Plan X is NOT premium`
3. Füge Plan-ID hinzu

---

## ?? Support & Hilfe

Bei Fragen:
1. Siehe Dokumentation (oben)
2. Prüfe Logs
3. Prüfe `REALTIME_API_VERIFICATION_README.md`
4. Prüfe `TROUBLESHOOTING.md` (falls vorhanden)

---

## ?? Zusammenfassung für Copilot

### Bei Code-Änderungen beachten:
1. ? Secrets nur in Environment Variables/User Secrets
2. ? API-basierte Verifizierung (keine Dateien)
3. ? Async/Await für alle I/O
4. ? Dependency Injection
5. ? Strukturiertes Logging
6. ? Cache für Performance (5 Min)
7. ? Thread-Safety bei Shared Resources
8. ? Error Handling mit Fail-Safe Defaults

### Bei Fragen zur Architektur:
- Siehe `ARCHITECTURE_DIAGRAM.md`
- Siehe `REALTIME_API_VERIFICATION_README.md`

### Bei Fragen zu Secrets:
- Siehe `SECRETS_MANAGEMENT.md`
- **Niemals in appsettings.json!**

---

**Letzte Aktualisierung:** 2025-01-15  
**Version:** 2.0 (API-basiert)  
**Maintainer:** @mbreukel
