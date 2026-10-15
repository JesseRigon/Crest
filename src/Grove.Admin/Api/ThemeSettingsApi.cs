#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Settings;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Grove.Admin.Api;

public sealed class ThemeSettingsApi : ApiContributor
{
    public override string Name => "ThemeSettings";

    public override string FeatureId => "Grove.Admin";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("theme");
        routes.MapGet("", ApiHandlers.Handle<ThemeSettingsHandlers>(h => h.Get()));
        routes.MapPut("", (HttpContext __http, [FromBody] ThemeSettings settings) => ApiHandlers.For<ThemeSettingsHandlers>(__http).Put(settings));
    }
}

public sealed class ThemeSettingsHandlers(ISiteService siteService, IAuthorizationService authorizationService) : ApiHandlers
{
    public async Task<IResult> Get()
    {
        var site = await siteService.GetSiteSettingsAsync();
        var settings = site.Properties["GroveTheme"]?.Deserialize<ThemeSettings>(JsonSerializerOptions.Web)
            ?? ThemeSettings.Default;

        return Ok(settings);
    }

    public async Task<IResult> Put(ThemeSettings settings)
    {
        if (!await authorizationService.AuthorizeAsync(User, SettingsPermissions.ManageSettings))
        {
            return Forbid();
        }

        var normalized = new ThemeSettings(
            string.IsNullOrWhiteSpace(settings.RadzenTheme) ? ThemeSettings.Default.RadzenTheme : settings.RadzenTheme.Trim(),
            settings.Tokens
                .Where(token => !string.IsNullOrWhiteSpace(token.Key) && !string.IsNullOrWhiteSpace(token.Value))
                .ToDictionary(token => token.Key.Trim(), token => token.Value.Trim(), StringComparer.OrdinalIgnoreCase));

        var site = await siteService.LoadSiteSettingsAsync();
        site.Properties["GroveTheme"] = JsonSerializer.SerializeToNode(normalized, JsonSerializerOptions.Web) ?? new JsonObject();
        await siteService.UpdateSiteSettingsAsync(site);

        return Ok(normalized);
    }
}
