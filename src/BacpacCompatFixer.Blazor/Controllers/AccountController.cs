using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BacpacCompatFixer.Blazor.Controllers;

[AllowAnonymous]
[Route("[controller]/[action]")]
public class AccountController : Controller
{
    [HttpGet]
    [HttpPost]
    public IActionResult SignOut()
    {
        var callbackUrl = Url.Page("/", pageHandler: null, values: null, protocol: Request.Scheme);
        
        return SignOut(
            new AuthenticationProperties { RedirectUri = callbackUrl },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
