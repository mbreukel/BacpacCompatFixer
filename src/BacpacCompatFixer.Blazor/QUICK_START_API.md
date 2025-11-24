# ?? Quick Start: API-basierte Subscription-Prüfung

## ? Setup in 3 Schritten

### 1?? Azure AD Client Secret erstellen

```
Azure Portal ? Azure Active Directory ? App registrations ? Deine App
? Certificates & secrets ? New client secret ? Kopiere Secret Value
```

### 2?? appsettings.json konfigurieren

```json
{
  "AzureAd": {
    "TenantId": "DEINE-TENANT-ID",
    "ClientId": "DEINE-CLIENT-ID",
    "ClientSecret": "DEIN-CLIENT-SECRET"  ? NEU!
  },
  "Marketplace": {
    "PremiumPlanIds": ["premium", "pro"],   ? Deine Plan-IDs
    "EnableCaching": true,                  ? Empfohlen
    "CacheDurationMinutes": 5
  }
}
```

### 3?? Fertig! ??

```csharp
// Bei jedem Login wird API abgefragt:
var status = await _purchaseService.VerifyPurchaseAsync(userEmail);
if (status.HasPurchased && status.Status == SubscriptionStatus.Active)
{
    // Premium User!
}
```

---

## ?? Wichtige Befehle

### Client Secret in User Secrets speichern (Development):
```bash
cd src\BacpacCompatFixer.Blazor
dotnet user-secrets set "AzureAd:ClientSecret" "your-secret-here"
```

### Logs anschauen:
```bash
dotnet run
# Schaue nach:
# [Information] Querying Marketplace API for user ...
# [Information] User ... has Active subscription with plan premium
```

### Build:
```bash
dotnet build
```

---

## ?? Wo finde ich die Plan-IDs?

```
Partner Center ? Marketplace offers ? Dein Angebot 
? Plan overview ? Kopiere "Plan ID"
```

Beispiele:
- `standard`
- `premium`
- `enterprise`
- `yearly-gold`

---

## ?? Wie funktioniert es?

```
User Login
    ?
Check Cache (5 Min.)
    ? (miss)
API Call: GET /api/saas/subscriptions
    ?
Find user's subscription
    ?
planId in PremiumPlanIds? ? Yes ? Premium (5GB)
                          ? No  ? Free (500MB)
```

---

## ?? Cache aktivieren/deaktivieren

### Mit Cache (empfohlen):
```json
"EnableCaching": true,
"CacheDurationMinutes": 5
```
- Weniger API-Calls
- Schnellere Logins
- Bei 100 Users: ~1.200 statt 6.000 API-Calls/Std

### Ohne Cache (100% Echtzeit):
```json
"EnableCaching": false
```
- Jeder Login = API-Call
- Immer aktuellster Status
- Nur für kleine Anwendungen (<10 Users)

---

## ?? Häufige Probleme

### ? "Failed to authenticate with Azure AD"
**Lösung:** Prüfe `TenantId`, `ClientId`, `ClientSecret`

### ? "No active subscription found"
**Lösung:** Prüfe:
1. User hat Subscription in Partner Center?
2. E-Mail-Adresse stimmt überein?
3. Subscription Status = "Subscribed"?

### ? User bekommt kein Premium
**Lösung:**
1. Prüfe `PremiumPlanIds` in appsettings.json
2. Logs: `Plan X is NOT premium`
3. Füge Plan-ID hinzu
4. Warte 5 Min (Cache) oder App neu starten

---

## ?? Testing

### Test 1: User ohne Subscription
```
Login ? Logs: "No active subscription" ? Free Tier (500MB)
```

### Test 2: User mit Premium
```
Login ? API Call ? "Plan premium is PREMIUM" ? Premium (5GB)
```

### Test 3: Cache
```
1. Login ? API Call
2. Logout ? Login (< 5 Min) ? "Returning cached"
3. Warte 6 Min ? Login ? Neuer API Call
```

---

## ?? Neue Dateien

| Datei | Beschreibung |
|-------|-------------|
| `MarketplaceAuthService.cs` | Azure AD Authentication |
| `MarketplaceApiService.cs` | API Calls zu Microsoft |
| `RealTimePurchaseVerificationService.cs` | Ersetzt alte Datei-Lösung |

---

## ? Vorteile der neuen Lösung

| Feature | Alt (Dateien) | Neu (API) |
|---------|--------------|-----------|
| Speicherung | JSON-Dateien | ? Keine |
| Aktualität | Nur bei Webhook | ? Immer |
| Skalierung | Problematisch | ? Perfekt |
| Load Balancing | File-Locks | ? Funktioniert |

---

## ?? Vollständige Doku

Siehe: `REALTIME_API_VERIFICATION_README.md`

---

## ?? Hilfe

1. Prüfe Logs (siehe oben)
2. Prüfe Azure AD Konfiguration
3. Teste API manuell:

```bash
curl -X POST https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token \
  -d "grant_type=client_credentials" \
  -d "client_id={client-id}" \
  -d "client_secret={secret}" \
  -d "scope=20e940b3-4c77-4b0b-9a53-9e16a1b010a7/.default"
```
