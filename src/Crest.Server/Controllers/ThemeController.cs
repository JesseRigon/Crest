using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Crest.Settings;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/theme")]
public sealed class ThemeController(ISiteService siteService, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var site = await siteService.GetSiteSettingsAsync();
        var settings = site.Properties["CrestTheme"]?.Deserialize<ThemeSettings>(JsonSerializerOptions.Web)
            ?? ThemeSettings.Default;

        return Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult<ThemeSettings>> Put(ThemeSettings settings)
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
        site.Properties["CrestTheme"] = JsonSerializer.SerializeToNode(normalized, JsonSerializerOptions.Web) ?? new JsonObject();
        await siteService.UpdateSiteSettingsAsync(site);

        return Ok(normalized);
    }
}
