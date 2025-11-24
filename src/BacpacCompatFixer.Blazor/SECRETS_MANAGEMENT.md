# ?? Secrets Management - BacpacCompatFixer

## ?? WICHTIG: Secrets-Konfiguration für dieses Repository

### Wo werden Secrets gespeichert?

**Dieses Repository verwendet:**
- ? **Azure App Service Environment Variables** (Production)
- ? **User Secrets** (Development)
- ? **NIEMALS in appsettings.json**

---

## ?? Setup-Anleitung

### 1. Lokale Entwicklung (User Secrets)

```bash
cd src\BacpacCompatFixer.Blazor

# Client Secret setzen
dotnet user-secrets set "AzureAd:ClientSecret" "your-secret-here"

# Verifizieren
dotnet user-secrets list
```

**Ausgabe sollte sein:**
```
AzureAd:ClientSecret = your-secret-here
```

### 2. Azure App Service (Production)

#### Option A: Azure Portal
```
1. Azure Portal ? Ihr App Service
2. Settings ? Configuration
3. Application settings ? + New application setting
4. Name: AzureAd__ClientSecret  (Doppelter Unterstrich!)
5. Value: your-client-secret
6. Save
```

#### Option B: Azure CLI
```bash
az webapp config appsettings set \
  --resource-group your-resource-group \
  --name your-app-name \
  --settings AzureAd__ClientSecret="your-client-secret"
```

#### Option C: Azure PowerShell
```powershell
Set-AzWebApp `
  -ResourceGroupName "your-resource-group" `
  -Name "your-app-name" `
  -AppSettings @{"AzureAd__ClientSecret" = "your-client-secret"}
```

---

## ?? appsettings.json Struktur

### ? Richtig (ohne Secret):
```json
{
  "AzureAd": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id"
    // ClientSecret kommt aus Environment Variables!
  }
}
```

### ? Falsch (Secret im Code):
```json
{
  "AzureAd": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "secret-here"  // ? NIEMALS SO!
  }
}
```

---

## ?? Wie funktioniert es?

### ASP.NET Core Konfiguration:

```csharp
// Program.cs lädt automatisch in dieser Reihenfolge:
1. appsettings.json
2. appsettings.{Environment}.json
3. User Secrets (Development)
4. Environment Variables (immer)
5. Command Line Arguments

// Environment Variables überschreiben appsettings.json!
```

### Beispiel:

```json
// appsettings.json
{
  "AzureAd": {
    "ClientId": "app-id-123"
  }
}

// Environment Variable (überschreibt!)
AzureAd__ClientSecret = "secret-xyz"

// Ergebnis in _configuration:
{
  "AzureAd": {
    "ClientId": "app-id-123",
    "ClientSecret": "secret-xyz"  ? Aus Environment!
  }
}
```

---

## ??? Security Best Practices

### ? DO:
- Secrets in Azure Key Vault speichern (noch besser!)
- User Secrets für lokale Entwicklung
- Environment Variables für Production
- `.gitignore` enthält `appsettings.Development.json` und `secrets.json`

### ? DON'T:
- Secrets in Git committen
- Secrets in appsettings.json
- Secrets in Code hardcoden
- Secrets in Logs ausgeben

---

## ?? Azure Key Vault (Empfohlen für Production)

### Setup:

```bash
# 1. Key Vault erstellen
az keyvault create \
  --name your-keyvault \
  --resource-group your-rg \
  --location westeurope

# 2. Secret hinzufügen
az keyvault secret set \
  --vault-name your-keyvault \
  --name AzureAd--ClientSecret \
  --value "your-secret"

# 3. App Service Zugriff gewähren
az webapp identity assign \
  --name your-app-name \
  --resource-group your-rg

# 4. Key Vault Access Policy
az keyvault set-policy \
  --name your-keyvault \
  --object-id <app-service-principal-id> \
  --secret-permissions get list
```

### Program.cs erweitern:

```csharp
// NuGet: Azure.Extensions.AspNetCore.Configuration.Secrets
builder.Configuration.AddAzureKeyVault(
    new Uri("https://your-keyvault.vault.azure.net/"),
    new DefaultAzureCredential());
```

---

## ?? Übersicht: Wo sind welche Secrets?

| Environment | ClientSecret | TenantId | ClientId |
|-------------|--------------|----------|----------|
| **Local Dev** | User Secrets | appsettings.json | appsettings.json |
| **Azure App Service** | Environment Var | appsettings.json | appsettings.json |
| **Docker** | Environment Var | appsettings.json | appsettings.json |
| **Production (ideal)** | Key Vault | Key Vault | Key Vault |

---

## ?? Testing

### Lokaler Test:
```bash
# 1. User Secret setzen
dotnet user-secrets set "AzureAd:ClientSecret" "test-secret"

# 2. App starten
dotnet run

# 3. Logs prüfen:
[Information] Successfully obtained Marketplace API access token
```

### Azure App Service Test:
```bash
# 1. Environment Variable prüfen
az webapp config appsettings list \
  --name your-app-name \
  --resource-group your-rg \
  | grep ClientSecret

# 2. Logs prüfen
az webapp log tail \
  --name your-app-name \
  --resource-group your-rg
```

---

## ?? Troubleshooting

### Problem: "Azure AD configuration is missing"

**Ursache:** `ClientSecret` nicht gefunden

**Lösung:**
```bash
# Lokale Entwicklung:
dotnet user-secrets set "AzureAd:ClientSecret" "your-secret"

# Azure App Service:
az webapp config appsettings set \
  --name your-app \
  --resource-group your-rg \
  --settings AzureAd__ClientSecret="your-secret"
```

### Problem: Secret wird nicht geladen

**Ursache:** Falsche Naming Convention

**Lösung:**
```bash
# ? Richtig (Doppelter Unterstrich):
AzureAd__ClientSecret

# ? Falsch (einfacher Unterstrich):
AzureAd_ClientSecret

# ? Falsch (Punkt):
AzureAd.ClientSecret
```

---

## ?? Checkliste

- [ ] User Secrets für Development konfiguriert
- [ ] `.gitignore` enthält `appsettings.Development.json`
- [ ] Azure App Service Environment Variable gesetzt
- [ ] Build erfolgreich
- [ ] Logs zeigen erfolgreiche Token-Authentifizierung
- [ ] Keine Secrets in Git committed

---

## ?? Wichtige Links

- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Safe storage of app secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Azure Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/)
- [Azure App Service Configuration](https://learn.microsoft.com/en-us/azure/app-service/configure-common)

---

**Letzte Aktualisierung:** 2025-01-15  
**Repository:** BacpacCompatFixer  
**Wichtig:** Diese Datei in Git committen als Dokumentation!
