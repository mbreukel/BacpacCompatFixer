using BacpacCompatFixer.Blazor.Components;
using BacpacCompatFixer.Blazor.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// Add authentication with Microsoft Identity
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        
        // Set the redirect URI after signout to home page
        options.SignedOutRedirectUri = "/";
        
        // Configure SignOut events
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProviderForSignOut = context =>
            {
                // Ensure post logout redirect URI is set correctly
                if (string.IsNullOrEmpty(context.ProtocolMessage.PostLogoutRedirectUri))
                {
                    context.ProtocolMessage.PostLogoutRedirectUri = context.Request.Scheme + "://" + context.Request.Host + context.Options.SignedOutCallbackPath;
                }
                return Task.CompletedTask;
            }
        };
    });

// Add authorization services
builder.Services.AddAuthorization();

// Add cascading authentication state
builder.Services.AddCascadingAuthenticationState();

// Add controllers for authentication UI
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

// Add HttpClient factory for JWT validation
builder.Services.AddHttpClient();

// Add JWT token validation service
builder.Services.AddScoped<IJwtTokenValidationService, JwtTokenValidationService>();

// Add purchase verification service
builder.Services.AddScoped<IPurchaseVerificationService, PurchaseVerificationService>();

// Add rate limiting service
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Add controllers for authentication BEFORE Blazor components
app.MapControllers();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
