#nullable enable

using System.Globalization;
using Crest.Api;
using Crest.Entities;
using Crest.Users.Localization.Models;
using Crest.Users.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace Crest.Users.Api;

/// <summary>The signed-in user's own default culture (<c>api/crest/localization/me</c>).</summary>
public sealed class UserCultureApi : ApiContributor
{
    public override string Name => "UserCulture";

    public override string FeatureId => "Crest.Users";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("localization/me");
        routes.MapGet("", ApiHandlers.Handle<UserCultureHandlers>(h => h.GetMyCultureAsync()));
        routes.MapPut("", (HttpContext __http, [FromBody] UserCulture request) => ApiHandlers.For<UserCultureHandlers>(__http).SetMyCultureAsync(request));
    }
}

public sealed class UserCultureHandlers(UserManager<IUser> userManager) : ApiHandlers
{
    // Self-service: the current user's own stored default culture
    // (Crest.Users.Localization's UserLocalizationSettings, on User.Properties —
    // see docs/localization.md). Deliberately scoped to "current user only", not
    // gated by LocalizationPermissions.ManageCultures/user-management permissions the way
    // the stock admin "edit user" screen is, since every signed-in user manages their own
    // preference here. Shared by the culture dropdown's "Save as default" action and the
    // Settings page's localization section — one write path, not duplicated.

    public async Task<IResult> GetMyCultureAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Forbid();
        }

        var user = await userManager.GetUserAsync(User) as User;
        if (user is null)
        {
            return NotFound();
        }

        user.TryGet<UserLocalizationSettings>(out var settings);
        return Ok(new UserCulture(settings?.Culture));
    }


    public async Task<IResult> SetMyCultureAsync(UserCulture request)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Forbid();
        }

        var user = await userManager.GetUserAsync(User) as User;
        if (user is null)
        {
            return NotFound();
        }

        string? culture = null;
        if (!string.IsNullOrWhiteSpace(request.Culture))
        {
            try
            {
                culture = CultureInfo.GetCultureInfo(request.Culture).Name;
            }
            catch (CultureNotFoundException)
            {
                return BadRequest("Culture must be a valid .NET/BCP 47 culture name.");
            }
        }

        user.Alter<UserLocalizationSettings>(settings => settings.Culture = culture);
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Ok(new UserCulture(culture));
    }
}

// Culture is null when the user has no stored default (falls through to the next
// resolution step — see docs/localization.md's resolution order).
public sealed record UserCulture(string? Culture);
