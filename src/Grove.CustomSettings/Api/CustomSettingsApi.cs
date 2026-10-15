#nullable enable

using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using Grove.Api;
using Grove.ContentManagement;
using Grove.CustomSettings.Services;
using Grove.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.CustomSettings.Api;

/// <summary>
/// The custom settings pages' API (<c>api/grove/custom-settings</c>): the settings types the caller
/// may manage, and saving one. A type's editor tree is the server's display endpoint
/// (<c>custom-settings/{type}/editor</c>).
/// </summary>
public sealed class CustomSettingsApi : ApiContributor
{
    public override string Name => "CustomSettings";

    public override string FeatureId => "Grove.CustomSettings";

    public override void Map(IEndpointRouteBuilder group)
    {
        var settings = group.MapGroup("custom-settings");
        settings.MapGet("", ListAsync);
        settings.MapPut("{type}", SaveAsync);
    }

    private static async Task<IResult> ListAsync([FromServices] CustomSettingsService service)
    {
        var list = new List<CustomSettingsType>();
        foreach (var type in await service.GetAllSettingsTypesAsync())
        {
            if (await service.CanUserCreateSettingsAsync(type))
            {
                list.Add(new CustomSettingsType(type.Name, type.DisplayName));
            }
        }

        return TypedResults.Ok(list);
    }

    private static async Task<IResult> SaveAsync([FromRoute] string type, [FromBody] CustomSettingsWrite request, [FromServices] CustomSettingsService service, [FromServices] ISiteService siteService)
    {
        var definition = await service.GetSettingsTypeAsync(type);
        if (definition is null) return TypedResults.NotFound();
        if (!await service.CanUserCreateSettingsAsync(definition)) return TypedResults.Forbid();

        var site = await siteService.LoadSiteSettingsAsync();
        var item = await service.GetSettingsAsync(site, definition);
        item.Content.Clear();
        item.Merge(request.Content?.DeepClone() ?? new JsonObject(), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        site.Properties[definition.Name] = JObject.FromObject(item);
        await siteService.UpdateSiteSettingsAsync(site);
        return TypedResults.NoContent();
    }
}

public sealed record CustomSettingsType(string Name, string DisplayName);

public sealed record CustomSettingsWrite(JsonObject? Content);
