# Azure AD Authentication Setup Guide

This guide explains how to configure Microsoft Identity (Azure AD/Entra ID) authentication for the BacpacCompatFixer Blazor application.

## Prerequisites

- An Azure subscription
- Access to Azure Portal (https://portal.azure.com)
- .NET 9 SDK installed

## Step 1: Register Application in Azure AD

1. Go to the [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** (or **Microsoft Entra ID**)
3. Select **App registrations** from the left menu
4. Click **+ New registration**
5. Fill in the registration form:
   - **Name**: `BacpacCompatFixer`
   - **Supported account types**: Choose one of:
     - `Accounts in any organizational directory (Any Azure AD directory - Multitenant) and personal Microsoft accounts` - for both personal and business accounts
     - `Accounts in any organizational directory (Any Azure AD directory - Multitenant)` - for business accounts only
   - **Redirect URI**: 
     - Type: Web
     - URI: `https://localhost:5001/signin-oidc` (for development)
     - Add production URL when deploying: `https://yourdomain.com/signin-oidc`
6. Click **Register**

## Step 2: Configure Authentication

1. After registration, you'll see the app overview page
2. Note down:
   - **Application (client) ID** - you'll need this for `ClientId` in appsettings.json
   - **Directory (tenant) ID** - you'll need this if using single tenant
3. Go to **Authentication** in the left menu
4. Under **Implicit grant and hybrid flows**, check:
   - ✅ **ID tokens** (used for hybrid flows)
5. Under **Logout URL**, add:
   - `https://localhost:5001/signout-callback-oidc` (for development)
   - Add production URL when deploying
6. Click **Save**

## Step 3: Create Client Secret

1. Go to **Certificates & secrets** in the left menu
2. Click **+ New client secret**
3. Add a description: `BacpacCompatFixer Secret`
4. Choose an expiration period (recommended: 24 months)
5. Click **Add**
6. **IMPORTANT**: Copy the **Value** immediately - you'll need this for `ClientSecret` in appsettings.json
7. ⚠️ You won't be able to see this value again after leaving the page

## Step 4: Configure API Permissions (Optional)

If you need to access Microsoft Graph API or other Microsoft services:

1. Go to **API permissions** in the left menu
2. Click **+ Add a permission**
3. Select **Microsoft Graph**
4. Choose **Delegated permissions**
5. Add required permissions (e.g., `User.Read`)
6. Click **Add permissions**
7. If required by your organization, click **Grant admin consent**

## Step 5: Update Application Configuration

1. Open `appsettings.json` in your Blazor project
2. Update the `AzureAd` section with your Client ID:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "common",
    "ClientId": "YOUR_CLIENT_ID_HERE",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  }
}
```

**Important Notes:**
- Replace `YOUR_CLIENT_ID_HERE` with the Application (client) ID from Step 2
- **DO NOT** add `ClientSecret` to `appsettings.json` - use User Secrets or environment variables instead (see Step 6)
- Use `"TenantId": "common"` to support both personal and business Microsoft accounts
- Use `"TenantId": "YOUR_TENANT_ID"` for single-tenant (organization-only) authentication
- Use `"TenantId": "organizations"` for multi-tenant business accounts only

## Step 6: Secure Your Secrets

**⚠️ NEVER commit secrets to source control!**

### For Development:
Use User Secrets to store sensitive configuration:

```bash
cd src/BacpacCompatFixer.Blazor
dotnet user-secrets init
dotnet user-secrets set "AzureAd:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "AzureAd:ClientSecret" "YOUR_CLIENT_SECRET"
```

### For Production:
- Use Azure Key Vault
- Use environment variables
- Use Azure App Service application settings

## Step 7: Test Authentication

1. Run the application:
   ```bash
   cd src/BacpacCompatFixer.Blazor
   dotnet run
   ```

2. Navigate to `https://localhost:5001`
3. Click **Sign in** in the navigation bar
4. You should be redirected to Microsoft login
5. Sign in with your Microsoft account
6. Grant consent if prompted
7. You should be redirected back to the application

## Troubleshooting

### Common Issues:

1. **Redirect URI mismatch**
   - Ensure the redirect URI in Azure AD matches exactly what's configured in your app
   - Check both development and production URLs

2. **Client secret expired**
   - Create a new client secret in Azure Portal
   - Update your application configuration

3. **Consent required**
   - Some organizations require admin consent for applications
   - Contact your Azure AD administrator

4. **Port conflicts**
   - If your app runs on a different port, update the redirect URIs in Azure AD

### Enable Detailed Logging:

Add to `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.Authentication": "Debug",
      "Microsoft.Identity": "Debug"
    }
  }
}
```

## Security Best Practices

1. **Use HTTPS**: Always use HTTPS in production
2. **Rotate Secrets**: Regularly rotate client secrets
3. **Least Privilege**: Only request necessary API permissions
4. **Monitor**: Enable logging and monitoring in Azure AD
5. **MFA**: Encourage users to enable Multi-Factor Authentication
6. **Conditional Access**: Consider implementing conditional access policies

## Marketplace Integration

For Microsoft Marketplace purchase verification, you'll need to:

1. Register your application in Partner Center
2. Implement the Marketplace SaaS fulfillment APIs
3. Integrate the purchase webhook to receive purchase notifications
4. Update the `PurchaseVerificationService` to call the actual Marketplace APIs

See [Microsoft Commercial Marketplace Documentation](https://docs.microsoft.com/en-us/azure/marketplace/) for more details.

## Additional Resources

- [Microsoft Identity Platform Documentation](https://docs.microsoft.com/en-us/azure/active-directory/develop/)
- [Microsoft Identity Web Library](https://github.com/AzureAD/microsoft-identity-web)
- [Azure AD B2C Documentation](https://docs.microsoft.com/en-us/azure/active-directory-b2c/)
- [Microsoft Commercial Marketplace](https://docs.microsoft.com/en-us/azure/marketplace/)
