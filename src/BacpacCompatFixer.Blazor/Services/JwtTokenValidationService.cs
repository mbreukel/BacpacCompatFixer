using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BacpacCompatFixer.Blazor.Services;

public interface IJwtTokenValidationService
{
    Task<bool> ValidateTokenAsync(string token);
    ClaimsPrincipal? GetClaimsFromToken(string token);
}

public class JwtTokenValidationService : IJwtTokenValidationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtTokenValidationService> _logger;
    private readonly HttpClient _httpClient;

    public JwtTokenValidationService(
        IConfiguration configuration,
        ILogger<JwtTokenValidationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            
            if (!handler.CanReadToken(token))
            {
                _logger.LogWarning("Invalid JWT token format");
                return false;
            }

            var jwtToken = handler.ReadJwtToken(token);
            
            // Get the tenant ID from the token
            var tenantId = jwtToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
            if (string.IsNullOrEmpty(tenantId))
            {
                _logger.LogWarning("No tenant ID found in token");
                return false;
            }

            // Get signing keys from Microsoft
            var keysUrl = $"https://login.microsoftonline.com/{tenantId}/discovery/v2.0/keys";
            var response = await _httpClient.GetStringAsync(keysUrl);
            var keys = new JsonWebKeySet(response);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"https://sts.windows.net/{tenantId}/",
                ValidateAudience = true,
                ValidAudience = _configuration["AzureAd:ClientId"],
                ValidateLifetime = true,
                IssuerSigningKeys = keys.Keys,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            handler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogError(ex, "Token validation failed");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation");
            return false;
        }
    }

    public ClaimsPrincipal? GetClaimsFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            
            if (!handler.CanReadToken(token))
            {
                return null;
            }

            var jwtToken = handler.ReadJwtToken(token);
            var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");
            return new ClaimsPrincipal(identity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting claims from token");
            return null;
        }
    }
}
