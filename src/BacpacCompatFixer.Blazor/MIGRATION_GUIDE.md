# Migration Guide: Datei-basiert ? API-basiert

## ?? Übersicht

Dieser Guide hilft dir beim Umstieg von der **dateibasierten** zur **API-basierten** Subscription-Prüfung.

## ?? Wichtige Änderungen

### Was sich NICHT ändert:
- ? Interface `IPurchaseVerificationService` bleibt gleich
- ? Dein bestehender Code funktioniert ohne Änderungen
- ? Webhook-Controller funktioniert weiterhin

### Was sich ändert:
- ? Keine `App_Data/Purchases/` Dateien mehr
- ? Stattdessen: Echtzeitabfrage der Microsoft Marketplace API
- ? Neue Konfiguration: `ClientSecret` benötigt

## ?? Migration in 5 Schritten

### Schritt 1: Backup erstellen (Optional)

```bash
# Sichere alte Purchase-Daten
xcopy "src\BacpacCompatFixer.Blazor\App_Data\Purchases" "Backup\Purchases" /E /I

# Die Dateien werden nicht mehr benötigt, aber als Backup sinnvoll
```

### Schritt 2: Azure AD Client Secret erstellen

1. Gehe zu [Azure Portal](https://portal.azure.com)
2. **Azure Active Directory** ? **App registrations**
3. Wähle deine App
4. **Certificates & secrets** ? **New client secret**
5. Beschreibung: `Marketplace API Access`
6. Expires: `24 months` (empfohlen)
7. **Add** ? **Kopiere den Secret Value!**

?? **Wichtig:** Du siehst den Secret nur EINMAL! Speichere ihn sicher.

### Schritt 3: appsettings.json erweitern

**Vorher:**
```json
{
  "AzureAd": {
    "TenantId": "common",
    "ClientId": "your-client-id"
  }
}
```

**Nachher:**
```json
{
  "AzureAd": {
    "TenantId": "common",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"  ? NEU
  },
  "Marketplace": {
    "PremiumPlanIds": ["premium", "pro"],  ? NEU (vorher hardcoded)
    "EnableCaching": true,                 ? NEU (empfohlen)
    "CacheDurationMinutes": 5              ? NEU
  }
}
```

### Schritt 4: User Secrets einrichten (Development)

**Für lokale Entwicklung** (empfohlen):

```bash
cd src\BacpacCompatFixer.Blazor

# Secret in User Secrets speichern (nicht in appsettings.json!)
dotnet user-secrets set "AzureAd:ClientSecret" "your-secret-here"

# Verifizieren
dotnet user-secrets list
```

**Für Produktion:**
- Azure Key Vault (empfohlen)
- Environment Variables
- Azure App Service Configuration

### Schritt 5: Deployment

```bash
# Build
dotnet build

# Test lokal
dotnet run

# Prüfe Logs:
# [Information] Successfully obtained Marketplace API access token
# [Information] Querying Marketplace API for user ...
```

## ?? Vergleich: Alt vs. Neu

### Alte Implementierung (Dateien):

```csharp
// src\BacpacCompatFixer.Blazor\Services\PurchaseVerificationService.cs
public async Task<UserPurchaseStatus> VerifyPurchaseAsync(string userId)
{
    var filePath = GetUserPurchaseFilePath(userId);
    
    if (File.Exists(filePath))
    {
        var json = await File.ReadAllTextAsync(filePath);
        // ...
    }
}
```

**Probleme:**
- ? Nur aktuell nach Webhook
- ? File-Locks bei vielen Usern
- ? Nicht skalierbar (Load Balancing)
- ? Datenverlust bei Server-Wechsel

### Neue Implementierung (API):

```csharp
// src\BacpacCompatFixer.Blazor\Services\RealTimePurchaseVerificationService.cs
public async Task<UserPurchaseStatus> VerifyPurchaseAsync(string userId)
{
    // Check cache (5 min)
    if (_cache.TryGetValue(cacheKey, out var cached))
        return cached;

    // Query Microsoft Marketplace API
    var subscription = await _marketplaceApi.GetSubscriptionByUserEmailAsync(userId);
    
    // ...
}
```

**Vorteile:**
- ? Immer aktueller Status
- ? Keine File-Locks
- ? Perfekt skalierbar
- ? Funktioniert mit Load Balancing
- ? Single Source of Truth (Microsoft)

## ?? Daten-Migration

### Müssen alte Dateien migriert werden?

**Nein!** Die API-basierte Lösung benötigt keine lokalen Daten.

**Aber:** Für die Übergangsphase kannst du optional einen "Hybrid-Modus" implementieren:

```csharp
public async Task<UserPurchaseStatus> VerifyPurchaseAsync(string userId)
{
    try
    {
        // Versuche zuerst API
        return await VerifyViaApiAsync(userId);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "API failed, falling back to file system");
        
        // Fallback auf alte Dateien
        return await VerifyViaFileAsync(userId);
    }
}
```

Das ist aber **nicht empfohlen** - die API-Lösung sollte ausreichen.

## ?? Testing nach Migration

### Test 1: Authentifizierung

```bash
# Teste ob Access Token geholt werden kann
dotnet run

# Erwarteter Log:
[Information] Successfully obtained Marketplace API access token, expires at ...
```

### Test 2: User ohne Subscription

```bash
# Login mit User ohne Marketplace-Subscription
# Erwarteter Log:
[Information] No active subscription found for user test@example.com
[Information] User test@example.com has no active subscription - Free tier
```

### Test 3: User mit Premium

```bash
# Login mit User mit Premium-Subscription
# Erwarteter Log:
[Information] Found active subscription for user premium@example.com: sub-123 with plan premium
[Information] User premium@example.com has Active subscription with plan premium (Premium: True)
```

### Test 4: Cache

```bash
# 1. Login
[Information] Querying Marketplace API in real-time for user test@example.com

# 2. Logout und sofort wieder Login (< 5 Min)
[Debug] Returning cached purchase status for test@example.com

# 3. Warte 6 Minuten, Login
[Information] Querying Marketplace API in real-time for user test@example.com
```

## ?? Security Checkliste

- [ ] Client Secret NICHT in Git committed
- [ ] User Secrets für Development eingerichtet
- [ ] Azure Key Vault für Production konfiguriert
- [ ] API Permissions in Azure AD geprüft
- [ ] HTTPS erzwungen (bereits in Program.cs)
- [ ] Logs enthalten keine Secrets

## ?? Rollback-Plan

Falls Probleme auftreten, kannst du zur alten Lösung zurückkehren:

### Schritt 1: Service umschalten

In `Program.cs`:

```csharp
// NEU (API-basiert):
builder.Services.AddScoped<IPurchaseVerificationService, RealTimePurchaseVerificationService>();

// ALT (Datei-basiert) - Rollback:
// builder.Services.AddScoped<IPurchaseVerificationService, PurchaseVerificationService>();
```

### Schritt 2: Rebuild & Deploy

```bash
dotnet build
dotnet run
```

Die alte Service-Datei wurde als Backup gespeichert:
- `PurchaseVerificationService.cs.old`

## ?? Performance-Verbesserungen

### Vorher (Dateien):

```
User Login ? File.Exists() ? File.ReadAllTextAsync() ? Deserialize
Zeit: ~5-20ms (abhängig von Disk I/O)
Problem: File-Locks bei vielen gleichzeitigen Logins
```

### Nachher (API + Cache):

```
User Login ? Cache-Check (< 1ms) ? Cache Hit!
Zeit: < 1ms

Bei Cache Miss:
User Login ? API Call (50-200ms) ? Cache für 5 Min
Zeit: 50-200ms (nur 1x pro 5 Min pro User)
```

## ?? Empfohlene Einstellungen

### Development:
```json
{
  "Marketplace": {
    "EnableCaching": false,      ? Kein Cache für Tests
    "CacheDurationMinutes": 0
  },
  "Logging": {
    "LogLevel": {
      "BacpacCompatFixer.Blazor.Services": "Debug"  ? Mehr Logs
    }
  }
}
```

### Production:
```json
{
  "Marketplace": {
    "EnableCaching": true,       ? Cache aktivieren
    "CacheDurationMinutes": 5    ? 5 Minuten optimal
  },
  "Logging": {
    "LogLevel": {
      "BacpacCompatFixer.Blazor.Services": "Information"
    }
  }
}
```

## ? Post-Migration Checkliste

- [ ] Client Secret erstellt und gespeichert
- [ ] appsettings.json aktualisiert
- [ ] User Secrets konfiguriert (Development)
- [ ] Build erfolgreich
- [ ] Test 1: Authentifizierung erfolgreich
- [ ] Test 2: User ohne Subscription ? Free Tier
- [ ] Test 3: User mit Premium ? Premium-Zugang
- [ ] Test 4: Cache funktioniert
- [ ] Logs geprüft
- [ ] Production deployed
- [ ] Alte `App_Data/Purchases/` Dateien können gelöscht werden (nach Backup!)

## ?? Häufige Probleme nach Migration

### Problem: "Azure AD configuration is missing"

**Ursache:** `ClientSecret` fehlt

**Lösung:**
```bash
dotnet user-secrets set "AzureAd:ClientSecret" "your-secret"
```

### Problem: "Failed to authenticate with Azure AD"

**Ursache:** Falsches Secret oder abgelaufen

**Lösung:**
1. Erstelle neues Client Secret in Azure Portal
2. Update appsettings.json oder User Secrets

### Problem: Alle Users bekommen Free Tier

**Ursache:** API gibt keine Subscriptions zurück

**Lösung:**
1. Prüfe API Permissions in Azure AD
2. Prüfe ob Subscriptions in Partner Center existieren
3. Prüfe Logs: `Retrieved X subscriptions from Marketplace API`

### Problem: Performance schlechter

**Ursache:** Cache deaktiviert

**Lösung:**
```json
{
  "Marketplace": {
    "EnableCaching": true,
    "CacheDurationMinutes": 5
  }
}
```

## ?? Support

Bei weiteren Fragen:
1. Prüfe `REALTIME_API_VERIFICATION_README.md` für Details
2. Prüfe `QUICK_START_API.md` für Quick Reference
3. Schau dir die Logs an (siehe oben)

## ?? Fertig!

Nach erfolgreicher Migration hast du:

- ? Keine lokalen Dateien mehr
- ? Echtzeitabfrage von Microsoft
- ? Perfekte Skalierbarkeit
- ? Cache für Performance
- ? Production-ready Solution

**Wichtig:** Alte `App_Data/Purchases/` Dateien können nach erfolgreicher Migration gelöscht werden!
