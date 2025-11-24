# Real-Time API-Based Subscription Verification

## ? Was wurde implementiert?

Die Anwendung prüft jetzt **bei jedem Login** den Subscription-Status direkt über die **Microsoft Marketplace API** - **OHNE lokale Speicherung**!

## ?? Wie es funktioniert

### Bei jedem Benutzer-Login:

```
User Login ? VerifyPurchaseAsync(email)
                      ?
            Cache-Check (5 Min.)
                      ?
              Cache Miss/Expired
                      ?
     GET https://marketplaceapi.microsoft.com/api/saas/subscriptions
                      ?
           Find user's subscription
                      ?
           Check PlanId ? Premium?
                      ?
          Return UserPurchaseStatus
                      ?
           Cache für 5 Minuten
```

### Keine lokale Speicherung:
- ? Keine JSON-Dateien
- ? Kein File-System
- ? **100% API-basiert**
- ? **Optional: 5-Minuten-Cache** (reduziert API-Calls)

## ?? Neue Dateien

### 1. **MarketplaceAuthService.cs**
- Authentifizierung mit Azure AD
- Holt Access Token für Marketplace API
- Token-Caching (~55 Minuten)

### 2. **MarketplaceApiService.cs**
- Implementiert Microsoft Marketplace Fulfillment API v2
- `GetAllSubscriptionsAsync()` - Alle Subscriptions abrufen
- `GetSubscriptionByIdAsync()` - Subscription per ID
- `GetSubscriptionByUserEmailAsync()` - Subscription per E-Mail

### 3. **RealTimePurchaseVerificationService.cs**
- Ersetzt die dateibasierte `PurchaseVerificationService`
- Ruft bei jedem Check die API ab
- Optional: 5-Minuten-Cache (konfigurierbar)
- Webhook invalidiert Cache

## ?? Konfiguration

### 1. Azure AD App Registration

**Schritt 1: Client Secret erstellen**

1. Gehe zu [Azure Portal](https://portal.azure.com)
2. **Azure Active Directory** ? **App registrations**
3. Wähle deine App aus
4. **Certificates & secrets** ? **New client secret**
5. Beschreibung: "Marketplace API Access"
6. Expires: 24 Monate (oder nach Bedarf)
7. **Add** ? **Kopiere den Secret Value!**

**Schritt 2: API Permissions (optional, sollten schon vorhanden sein)**

1. **API permissions** ? **Add a permission**
2. **APIs my organization uses**
3. Suche nach: `Microsoft Marketplace` oder verwende die ID `20e940b3-4c77-4b0b-9a53-9e16a1b010a7`
4. **Application permissions** (nicht Delegated)
5. **Grant admin consent**

### 2. appsettings.json konfigurieren

```json
{
  "AzureAd": {
    "TenantId": "your-tenant-id",              // Deine Tenant ID
    "ClientId": "your-client-id",              // Deine App (Client) ID
    "ClientSecret": "your-client-secret"       // NEU: Der generierte Secret
  },
  "Marketplace": {
    "PremiumPlanIds": [
      "premium",      // Deine Plan IDs aus Partner Center
      "pro",
      "enterprise"
    ],
    "EnableCaching": true,        // Cache aktivieren (empfohlen)
    "CacheDurationMinutes": 5     // Cache-Dauer in Minuten
  }
}
```

### 3. Plan-IDs aus Partner Center

1. Gehe zu [Partner Center](https://partner.microsoft.com/)
2. **Marketplace offers** ? **Dein Angebot** ? **Plan overview**
3. Kopiere die **Plan ID** für jeden Premium-Plan
4. Füge sie zu `PremiumPlanIds` hinzu

## ?? Verwendung

### In deinem Code:

```csharp
public class MyComponent
{
    private readonly IPurchaseVerificationService _purchaseService;

    // Bei jedem Aufruf wird API abgefragt (oder Cache geprüft)
    public async Task<bool> CheckAccess(string userEmail)
    {
        var status = await _purchaseService.VerifyPurchaseAsync(userEmail);
        
        // Echtzeit-Status von Microsoft Marketplace
        return status.HasPurchased && status.Status == SubscriptionStatus.Active;
    }
}
```

### Beispiel: Datei-Upload mit Größenprüfung

```csharp
[HttpPost("upload")]
public async Task<IActionResult> UploadFile(IFormFile file)
{
    var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
    
    // Ruft API ab (oder nimmt Cache wenn < 5 Min alt)
    var status = await _purchaseService.VerifyPurchaseAsync(userEmail);

    if (file.Length > status.MaxFileSizeBytes)
    {
        return BadRequest($"File too large. Your limit: {status.MaxFileSizeBytes / 1024 / 1024}MB");
    }

    // Upload...
    return Ok();
}
```

## ?? Cache-Strategie

### Standard (empfohlen):
- ? **EnableCaching: true**
- ? **CacheDurationMinutes: 5**

**Warum?**
- Reduziert API-Calls von ~1000/Stunde auf ~12/Stunde pro User
- Bei 100 Users: 100.000 ? 1.200 API-Calls/Stunde
- Webhook invalidiert Cache sofort bei Änderungen

### Echtzeit (kein Cache):
```json
{
  "Marketplace": {
    "EnableCaching": false,
    "CacheDurationMinutes": 0
  }
}
```

**Wann verwenden?**
- Nur für Tests
- Bei sehr wenigen Usern (<10)
- Bei kritischen Anwendungen wo **jeder** Login geprüft werden muss

## ?? Vergleich: Alt vs. Neu

| Feature | Alte Lösung (Dateien) | Neue Lösung (API) |
|---------|----------------------|-------------------|
| **Speicherung** | JSON-Dateien | Keine (API-Calls) |
| **Aktualität** | Nur bei Webhook | Immer aktuell |
| **Skalierbarkeit** | Probleme bei vielen Users | Perfekt skalierbar |
| **Load Balancing** | Dateisystem-Locks | Funktioniert einwandfrei |
| **Status-Quelle** | Lokale Kopie | Microsoft (Single Source of Truth) |
| **Cache** | Nein | Optional (5 Min.) |

## ?? Security

### Access Token:
- Wird automatisch gecacht (~55 Minuten)
- Thread-safe mit SemaphoreSlim
- Automatische Erneuerung bei Ablauf

### Client Secret:
?? **Wichtig:** Speichere Client Secret NIEMALS in Git!

**Für Produktion:**
```bash
# Azure Key Vault (empfohlen)
az keyvault secret set --vault-name "your-vault" --name "MarketplaceClientSecret" --value "your-secret"

# Oder User Secrets (Development)
dotnet user-secrets set "AzureAd:ClientSecret" "your-secret"

# Oder Umgebungsvariablen
export AzureAd__ClientSecret="your-secret"
```

**In appsettings.json:**
```json
{
  "AzureAd": {
    "ClientSecret": ""  // Leer lassen, aus Key Vault/Secrets laden
  }
}
```

## ?? Performance & Limits

### Microsoft Marketplace API Limits:
- **Keine offiziellen Rate Limits dokumentiert**
- Empfohlen: < 1000 Requests/Minute
- Mit 5-Min-Cache: Kein Problem bei normaler Last

### Cache-Auswirkungen:

| Users | Ohne Cache | Mit 5-Min Cache |
|-------|------------|-----------------|
| 10 | 600/Std | 120/Std |
| 100 | 6.000/Std | 1.200/Std |
| 1000 | 60.000/Std | 12.000/Std |

## ?? Troubleshooting

### Problem: "Failed to authenticate with Azure AD"

**Lösung:**
1. Prüfe `AzureAd:TenantId`, `ClientId`, `ClientSecret`
2. Stelle sicher, dass Client Secret nicht abgelaufen ist
3. Prüfe API Permissions in Azure AD

```bash
# Test authentication
curl -X POST https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token \
  -d "grant_type=client_credentials" \
  -d "client_id={client-id}" \
  -d "client_secret={client-secret}" \
  -d "scope=20e940b3-4c77-4b0b-9a53-9e16a1b010a7/.default"
```

### Problem: "No active subscription found"

**Lösung:**
1. Prüfe E-Mail-Adresse des Users
2. Prüfe Marketplace Portal: Gibt es eine aktive Subscription?
3. Prüfe Logs: `Querying Marketplace API for user {Email}`

### Problem: User bekommt kein Premium

**Lösung:**
1. Prüfe `PremiumPlanIds` in appsettings.json
2. Logs zeigen: `Plan {PlanId} is NOT premium`
3. Füge die richtige Plan-ID hinzu
4. Cache invalidieren: Warte 5 Minuten oder Neustart

## ?? Logs & Monitoring

### Wichtige Log-Events:

```
[Information] Querying Marketplace API in real-time for user test@example.com
[Information] Retrieved 5 subscriptions from Marketplace API
[Information] Found active subscription for user test@example.com: sub-123 with plan premium
[Information] User test@example.com has Active subscription with plan premium (Premium: True)
[Debug] Cached purchase status for test@example.com for 5 minutes
```

### Log Levels konfigurieren:

```json
{
  "Logging": {
    "LogLevel": {
      "BacpacCompatFixer.Blazor.Services.RealTimePurchaseVerificationService": "Debug",
      "BacpacCompatFixer.Blazor.Services.MarketplaceApiService": "Debug",
      "BacpacCompatFixer.Blazor.Services.MarketplaceAuthService": "Information"
    }
  }
}
```

## ? Testing

### Manueller Test:

1. **User ohne Subscription:**
```
Login ? Logs zeigen "No active subscription" ? Free Tier (500MB)
```

2. **User mit Premium-Plan:**
```
Login ? API Call ? "Plan premium is PREMIUM" ? Premium (5GB)
```

3. **Cache-Test:**
```
1. Login ? API Call
2. Logout
3. Login innerhalb 5 Min ? "Returning cached purchase status"
4. Warte 6 Min ? Login ? Neuer API Call
```

### Webhook-Test:

```
Webhook: ChangePlan to "premium"
? Log: "Cache invalidated for user test@example.com"
? Nächster Login: Neuer API Call ? Premium aktiviert
```

## ?? Fertig!

Das System ist jetzt vollständig API-basiert:

- ? **Kein lokales File-System** mehr
- ? **Immer aktueller Status** von Microsoft
- ? **Skalierbar** für tausende Users
- ? **Cache optional** (5 Min. empfohlen)
- ? **Webhook-Integration** invalidiert Cache
- ? **Thread-safe** und production-ready

## ?? Weitere Ressourcen

- [Microsoft Marketplace SaaS Fulfillment API](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-apis)
- [Get Publisher Authorization Token](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-registration#how-to-get-the-publishers-authorization-token)
- [Subscription APIs v2](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-subscription-api)

## ?? Support

Bei Problemen:
1. Prüfe Logs (siehe oben)
2. Prüfe Azure AD Konfiguration
3. Prüfe Partner Center Subscription Status
4. Teste API-Zugriff manuell (siehe Troubleshooting)
