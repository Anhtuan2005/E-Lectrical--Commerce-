using System.Security.Claims;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EcommerceApp.Services;

public class MinimalUserClaimsPrincipalFactory : IUserClaimsPrincipalFactory<ApplicationUser>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IdentityOptions _options;

    public MinimalUserClaimsPrincipalFactory(UserManager<ApplicationUser> userManager, IOptions<IdentityOptions> options)
    {
        _userManager = userManager;
        _options = options.Value;
    }

    public async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var identity = new ClaimsIdentity(
            IdentityConstants.ApplicationScheme,
            _options.ClaimsIdentity.UserNameClaimType,
            _options.ClaimsIdentity.RoleClaimType);

        var userId = await _userManager.GetUserIdAsync(user);
        var userName = await _userManager.GetUserNameAsync(user);

        identity.AddClaim(new Claim(_options.ClaimsIdentity.UserIdClaimType, userId));
        if (!string.IsNullOrWhiteSpace(userName))
        {
            identity.AddClaim(new Claim(_options.ClaimsIdentity.UserNameClaimType, userName));
        }

        if (_userManager.SupportsUserEmail)
        {
            var email = await _userManager.GetEmailAsync(user);
            if (!string.IsNullOrWhiteSpace(email))
            {
                identity.AddClaim(new Claim(_options.ClaimsIdentity.EmailClaimType, email));
            }
        }

        if (_userManager.SupportsUserSecurityStamp)
        {
            identity.AddClaim(new Claim(
                _options.ClaimsIdentity.SecurityStampClaimType,
                await _userManager.GetSecurityStampAsync(user)));
        }

        if (_userManager.SupportsUserRole)
        {
            foreach (var role in await _userManager.GetRolesAsync(user))
            {
                identity.AddClaim(new Claim(_options.ClaimsIdentity.RoleClaimType, role));
            }
        }

        return new ClaimsPrincipal(identity);
    }
}
