using System.Text.Json.Serialization;

namespace BacpacCompatFixer.Blazor.Services;

/// <summary>
/// Service for authenticating with Microsoft Marketplace APIs
/// </summary>
public interface IMarketplaceAuthService
{
    /// <summary>
    /// Get access token for Microsoft Marketplace API calls
    /// </summary>
    Task<string> GetAccessTokenAsync();
}

/// <summary>
/// Authenticates with Azure AD to obtain access tokens for Marketplace API
/// </summary>
public class MarketplaceAuthService : IMarketplaceAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MarketplaceAuthService> _logger;
    
    // Cache token to avoid unnecessary API calls
    private string? _cachedToken;
    private DateTime _tokenExpiration;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public MarketplaceAuthService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<MarketplaceAuthService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        // Return cached token if still valid
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration)
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration)
            {
                return _cachedToken;
            }

            var tenantId = _configuration["AzureAd:TenantId"];
            var clientId = _configuration["AzureAd:ClientId"];
            var clientSecret = _configuration["AzureAd:ClientSecret"];

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException(
                    "Azure AD configuration is missing. Please configure TenantId, ClientId, and ClientSecret in appsettings.json");
            }

            var httpClient = _httpClientFactory.CreateClient();
            var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("scope", "20e940b3-4c77-4b0b-9a53-9e16a1b010a7/.default") // Marketplace API resource
            });

            _logger.LogDebug("Requesting access token from Azure AD for tenant {TenantId}", tenantId);

            var response = await httpClient.PostAsync(tokenEndpoint, requestBody);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(responseContent);

            if (tokenResponse?.AccessToken == null)
            {
                throw new InvalidOperationException("Failed to retrieve access token from Azure AD.");
            }

            // Cache token (valid for ~55 minutes, token expires in 60)
            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 300); // 5 min buffer

            _logger.LogInformation("Successfully obtained Marketplace API access token, expires at {ExpirationTime}", _tokenExpiration);
            return _cachedToken;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while obtaining Marketplace API access token");
            throw new InvalidOperationException("Failed to authenticate with Azure AD. Please check your credentials.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while obtaining Marketplace API access token");
            throw;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }
}
