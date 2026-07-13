using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UserLoginApp.Web.Pages;

public class LogoutModel : PageModel
{
    public async Task OnGetAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        // This triggers redirect to Keycloak end-session, then back to SignedOutRedirectUri ("/")
        await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
    }
}
