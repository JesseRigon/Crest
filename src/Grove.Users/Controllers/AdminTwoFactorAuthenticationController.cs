using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Grove.Admin;
using Grove.Modules;
using Grove.Mvc.Utilities;

namespace Grove.Users.Controllers;

[Admin]
[Feature(UserConstants.Features.TwoFactorAuthentication)]
public sealed class AdminTwoFactorAuthenticationController : Controller
{
    private readonly UserManager<IUser> _userManager;
    private readonly IAuthorizationService _authorizationService;

    public AdminTwoFactorAuthenticationController(
        UserManager<IUser> userManager,
        IAuthorizationService authorizationService)
    {
        _userManager = userManager;
        _authorizationService = authorizationService;
    }

    public async Task<IActionResult> Disable(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UsersPermissions.DisableTwoFactorAuthenticationForUsers))
        {
            return Forbid();
        }

        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
        {
            return NotFound();
        }



        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            await _userManager.SetTwoFactorEnabledAsync(user, false);
        }

        return LocalRedirect($"~/{HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Grove.Admin.AdminOptions>>().Value.AdminUrlPrefix.Trim('/')}/Users/Index");
    }
}
