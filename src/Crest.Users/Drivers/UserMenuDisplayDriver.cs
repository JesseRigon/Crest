using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.Admin;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Settings;
using Crest.Users.Models;

namespace Crest.Users.Drivers;

public sealed class UserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;

    public UserMenuDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ISiteService siteService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _siteService = siteService;
    }

    public override async Task<IDisplayResult> DisplayAsync(UserMenu model, BuildDisplayContext context)
    {
        var results = new List<IDisplayResult>
        {
            View("UserMenuItems__Title", model)
            .Location(PlatformConstants.DisplayType.Detail, "Header:5")
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Header:5")
            .Differentiator("Title"),

            View("UserMenuItems__SignedUser", model)
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:1")
            .Differentiator("SignedUser"),

            View("UserMenuItems__Profile", model)
            .Location(PlatformConstants.DisplayType.Detail, "Content:5")
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:5")
            .Differentiator("Profile"),

            View("UserMenuItems__SignOut", model)
            .Location(PlatformConstants.DisplayType.Detail, "Content:100")
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:100")
            .Differentiator("SignOut"),
        };

        var loginSettings = await _siteService.GetSettingsAsync<LoginSettings>();

        if (await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AdminPermissions.AccessAdminPanel))
        {
            results.Add(View("UserMenuItems__Dashboard", model)
                .Location(PlatformConstants.DisplayType.Detail, "Content:1.1")
                .Differentiator("Dashboard"));

            if (!loginSettings.DisableLocalLogin)
            {
                results.Add(View("UserMenuItems__ChangePassword", model)
                    .Location(PlatformConstants.DisplayType.Detail, "Content:10")
                    .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:10")
                    .Differentiator("ChangePassword"));
            }
        }
        else
        {
            if (!loginSettings.DisableLocalLogin)
            {
                results.Add(View("UserMenuItems__ChangePassword", model)
                .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:10")
                .Differentiator("ChangePassword"));
            }
        }

        return Combine(results);
    }
}
