# Deployment Guide for BacpacCompatFixer Blazor Application

This guide provides instructions for deploying the BacpacCompatFixer Blazor application to Azure App Service.

## Prerequisites

- Azure subscription
- Azure CLI installed (or use Azure Cloud Shell)
- .NET 9 SDK installed
- Azure AD application configured (see [AZURE_AD_SETUP.md](AZURE_AD_SETUP.md))

## Option 1: Deploy to Azure App Service using Azure CLI

### Step 1: Create Azure Resources

```bash
# Set variables
RESOURCE_GROUP="rg-bacpacfixer"
LOCATION="westeurope"
APP_SERVICE_PLAN="asp-bacpacfixer"
WEB_APP_NAME="bacpacfixer-app"  # Must be globally unique

# Login to Azure
az login

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create App Service Plan (Linux, .NET 9)
az appservice plan create \
    --name $APP_SERVICE_PLAN \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --is-linux \
    --sku B1

# Create Web App
az webapp create \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --plan $APP_SERVICE_PLAN \
    --runtime "DOTNET|9.0"
```

### Step 2: Configure Application Settings

```bash
# Set Azure AD configuration
az webapp config appsettings set \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
        AzureAd__Instance="https://login.microsoftonline.com/" \
        AzureAd__TenantId="common" \
        AzureAd__ClientId="YOUR_CLIENT_ID" \
        AzureAd__ClientSecret="YOUR_CLIENT_SECRET" \
        AzureAd__CallbackPath="/signin-oidc" \
        AzureAd__SignedOutCallbackPath="/signout-callback-oidc"

# Configure HTTPS
az webapp update \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --https-only true
```

### Step 3: Update Azure AD Redirect URIs

After creating the web app, update your Azure AD app registration:

1. Go to Azure Portal → Azure Active Directory → App registrations
2. Select your application
3. Go to Authentication
4. Add Redirect URIs:
   - `https://$WEB_APP_NAME.azurewebsites.net/signin-oidc`
5. Add Logout URL:
   - `https://$WEB_APP_NAME.azurewebsites.net/signout-callback-oidc`
6. Click Save

### Step 4: Deploy the Application

```bash
# Navigate to the Blazor project directory
cd src/BacpacCompatFixer.Blazor

# Publish the application
dotnet publish -c Release -o ./publish

# Create a deployment package
cd publish
zip -r ../deploy.zip .
cd ..

# Deploy to Azure
az webapp deployment source config-zip \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --src deploy.zip

# Restart the web app
az webapp restart \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP
```

### Step 5: Verify Deployment

```bash
# Get the web app URL
az webapp show \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --query defaultHostName \
    --output tsv
```

Visit `https://$WEB_APP_NAME.azurewebsites.net` to test the application.

## Option 2: Deploy using Visual Studio

1. Open the solution in Visual Studio
2. Right-click on `BacpacCompatFixer.Blazor` project
3. Select **Publish**
4. Choose **Azure** → **Azure App Service (Linux)**
5. Sign in to your Azure account
6. Create new or select existing App Service
7. Click **Publish**

## Option 3: Deploy using GitHub Actions

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

env:
  AZURE_WEBAPP_NAME: bacpacfixer-app
  AZURE_WEBAPP_PACKAGE_PATH: './src/BacpacCompatFixer.Blazor'
  DOTNET_VERSION: '9.0.x'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Publish
      run: dotnet publish ${{ env.AZURE_WEBAPP_PACKAGE_PATH }} -c Release -o ./publish
    
    - name: Deploy to Azure Web App
      uses: azure/webapps-deploy@v2
      with:
        app-name: ${{ env.AZURE_WEBAPP_NAME }}
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
        package: ./publish
```

To use GitHub Actions:
1. Download the publish profile from Azure Portal
2. Add it as a secret named `AZURE_WEBAPP_PUBLISH_PROFILE` in your GitHub repository
3. Push to the main branch to trigger deployment

## Post-Deployment Configuration

### Configure Storage for Purchase Data

By default, purchase data is stored in the App Service file system. For production:

1. **Option A: Azure Blob Storage**
   - Create Azure Storage Account
   - Update `PurchaseVerificationService` to use Blob Storage
   - Add connection string to app settings

2. **Option B: Azure SQL Database**
   - Create Azure SQL Database
   - Create purchases table
   - Update `PurchaseVerificationService` to use EF Core

### Enable Application Insights

```bash
# Create Application Insights
az monitor app-insights component create \
    --app bacpacfixer-insights \
    --location $LOCATION \
    --resource-group $RESOURCE_GROUP

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
    --app bacpacfixer-insights \
    --resource-group $RESOURCE_GROUP \
    --query instrumentationKey \
    --output tsv)

# Configure Web App
az webapp config appsettings set \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
        APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=$INSTRUMENTATION_KEY"
```

### Configure Custom Domain

```bash
# Add custom domain
az webapp config hostname add \
    --webapp-name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --hostname yourdomain.com

# Configure SSL certificate (managed certificate)
az webapp config ssl bind \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --certificate-thumbprint auto \
    --ssl-type SNI
```

### Enable Continuous Deployment

For continuous deployment from GitHub:

```bash
az webapp deployment source config \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --repo-url https://github.com/yourusername/BacpacCompatFixer \
    --branch main \
    --manual-integration
```

## Security Checklist

- [ ] Azure AD authentication configured correctly
- [ ] HTTPS enforced
- [ ] Client secrets stored in Azure Key Vault (not in config files)
- [ ] Application Insights enabled for monitoring
- [ ] Rate limiting configured
- [ ] CORS policies configured if needed
- [ ] Custom domain with SSL certificate
- [ ] Backup strategy in place

## Troubleshooting

### Application won't start
- Check Application Insights or App Service logs
- Verify all required app settings are configured
- Ensure Azure AD redirect URIs are correct

### Authentication not working
- Verify Azure AD configuration
- Check redirect URIs match exactly
- Ensure client secret hasn't expired

### File upload failures
- Check App Service storage quota
- Verify temp directory permissions
- Check file size limits in configuration

### View Logs

```bash
# Stream logs
az webapp log tail \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP

# Download logs
az webapp log download \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --log-file app-logs.zip
```

## Scaling

To scale the application:

```bash
# Scale up (vertical scaling)
az appservice plan update \
    --name $APP_SERVICE_PLAN \
    --resource-group $RESOURCE_GROUP \
    --sku P1V2

# Scale out (horizontal scaling)
az appservice plan update \
    --name $APP_SERVICE_PLAN \
    --resource-group $RESOURCE_GROUP \
    --number-of-workers 2
```

## Backup and Disaster Recovery

```bash
# Create backup
az webapp config backup create \
    --resource-group $RESOURCE_GROUP \
    --webapp-name $WEB_APP_NAME \
    --backup-name backup1 \
    --container-url "<SAS-URL-FOR-BACKUP-CONTAINER>"

# List backups
az webapp config backup list \
    --resource-group $RESOURCE_GROUP \
    --webapp-name $WEB_APP_NAME

# Restore from backup
az webapp config backup restore \
    --resource-group $RESOURCE_GROUP \
    --webapp-name $WEB_APP_NAME \
    --backup-name backup1
```

## Cost Optimization

- Use B1 (Basic) or S1 (Standard) for development/testing
- Use P1V2 or higher for production
- Enable auto-scaling based on metrics
- Consider Azure Functions for occasional workloads
- Use Azure CDN for static assets

## Monitoring and Alerts

Set up alerts for:
- High CPU usage
- High memory usage
- Error rate threshold
- Response time threshold
- Authentication failures

```bash
# Create alert for high CPU
az monitor metrics alert create \
    --name high-cpu-alert \
    --resource-group $RESOURCE_GROUP \
    --scopes /subscriptions/<subscription-id>/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/$WEB_APP_NAME \
    --condition "avg Percentage CPU > 80" \
    --description "Alert when CPU exceeds 80%"
```

## Additional Resources

- [Azure App Service Documentation](https://docs.microsoft.com/en-us/azure/app-service/)
- [Deploy ASP.NET Core apps to Azure App Service](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/azure-apps/)
- [Azure AD Authentication](https://docs.microsoft.com/en-us/azure/active-directory/develop/)
