#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Environment.Shell;
using Grove.Entities;
using Grove.Settings;
using Grove.Users;
using Grove.Users.Models;

namespace Grove.Users.Api;

public sealed class LoginSettingsApi : ApiContributor
{
    public override string Name => "LoginSettings";

    public override string FeatureId => "Grove.Users";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("settings/login");
        routes.MapGet("", ApiHandlers.Handle<LoginSettingsHandlers>(h => h.GetAsync()));
        routes.MapPut("", (HttpContext __http, [FromBody] LoginSettingsModel request) => ApiHandlers.For<LoginSettingsHandlers>(__http).SaveAsync(request));
    }
}

public sealed class LoginSettingsHandlers(ISiteService sites, IShellReleaseManager releaseManager, IAuthorizationService authorization) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ManageUsers)) return Forbid();
        return Ok(LoginSettingsModel.From(
            await sites.GetSettingsAsync<LoginSettings>(),
            await sites.GetSettingsAsync<TwoFactorLoginSettings>(),
            await sites.GetSettingsAsync<ExternalLoginSettings>()));
    }

    public async Task<IResult> SaveAsync([FromBody] LoginSettingsModel request)
    {
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ManageUsers)) return Forbid();
        if (request.NumberOfRecoveryCodesToGenerate < 1)
            return BadRequest("The number of recovery codes must be at least one.");

        var site = await sites.LoadSiteSettingsAsync();
        site.Alter<LoginSettings>(settings => Copy(request.ToLoginSettings(), settings));
        site.Alter<TwoFactorLoginSettings>(settings => Copy(request.ToTwoFactorLoginSettings(), settings));
        site.Alter<ExternalLoginSettings>(settings => Copy(request.ToExternalLoginSettings(), settings));
        await sites.UpdateSiteSettingsAsync(site);

        // These settings participate in authentication and must take effect for the next request.
        releaseManager.RequestRelease();
        return Ok(request);
    }

    private static void Copy<T>(T source, T destination)
    {
        foreach (var property in typeof(T).GetProperties().Where(property => property.CanRead && property.CanWrite))
            property.SetValue(destination, property.GetValue(source));
    }
}
