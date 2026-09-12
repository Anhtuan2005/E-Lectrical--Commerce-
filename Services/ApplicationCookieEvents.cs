using EcommerceApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace EcommerceApp.Services;

public sealed class ApplicationCookieEvents(
    SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> users) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        // Validate on every authenticated request so admin locks/revocations take effect on the next request.
        var user = await signInManager.ValidateSecurityStampAsync(context.Principal);
        if (user is null || await users.IsLockedOutAsync(user))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }
        await SecurityStampValidator.ValidatePrincipalAsync(context);
    }
}
