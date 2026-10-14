using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Components;
using Crest.Security.Permissions;
using Crest.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Controllers;

/// <summary>A custom settings type the caller may manage.</summary>
public sealed record CustomSettingsType(string Name, string DisplayName);

/// <summary>The settings item's edited content (as the editor tree's ContentEditor publishes it).</summary>
public sealed record CustomSettingsWrite(JsonObject? Content);

/// <summary>
/// Custom settings (Crest.CustomSettings: content types of the CustomSettings stereotype, one item
/// per type kept in the site settings) on the content editor tree - the native page replacing the
/// MVC settings group. Each type takes its own permission, ManageCustomSettings_{type}, which
/// ManageResourceSettings implies, as the module declares it.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/custom-settings")]
public sealed class CustomSettingsController(
    IContentDefinitionManager definitions,
    IContentManager contentManager,
    ISiteService siteService,
    IAuthorizationService authorization) : ControllerBase
{
    public const string Stereotype = "CustomSettings";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomSettingsType>>> ListAsync()
    {
        var list = new List<CustomSettingsType>();
        foreach (var type in await SettingsTypesAsync())
        {
            if (await CanManageAsync(type))
            {
                list.Add(new CustomSettingsType(type.Name, type.DisplayName));
            }
        }

        return Ok(list);
    }

    [HttpGet("{type}/editor")]
    public async Task<ActionResult<DisplayTree>> EditorAsync(string type, [FromServices] Crest.Display.ContentEditorTreeBuilder editors, CancellationToken cancellationToken)
    {
        var definition = await SettingsTypeAsync(type);
        if (definition is null) return NotFound();
        if (!await CanManageAsync(definition)) return Forbid();

        var tree = await editors.BuildAsync(await LoadAsync(await siteService.GetSiteSettingsAsync(), definition), cancellationToken);
        return tree is null ? NotFound() : Ok(tree);
    }

    [HttpPut("{type}")]
    public async Task<IActionResult> SaveAsync(string type, [FromBody] CustomSettingsWrite request)
    {
        var definition = await SettingsTypeAsync(type);
        if (definition is null) return NotFound();
        if (!await CanManageAsync(definition)) return Forbid();

        var site = await siteService.LoadSiteSettingsAsync();
        var item = await LoadAsync(site, definition);
        item.Content.Clear();
        item.Merge(request.Content?.DeepClone() ?? new JsonObject(), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        site.Properties[definition.Name] = JObject.FromObject(item);
        await siteService.UpdateSiteSettingsAsync(site);
        return NoContent();
    }

    // As CustomSettingsService does: a fresh item of the current definition, merged with what is stored.
    private async Task<ContentItem> LoadAsync(ISite site, ContentTypeDefinition definition)
    {
        var item = await contentManager.NewAsync(definition.Name);
        if (site.Properties.TryGetPropertyValue(definition.Name, out var stored) && stored is not null)
        {
            item.Merge(stored.ToObject<ContentItem>());
        }

        return item;
    }

    private async Task<IEnumerable<ContentTypeDefinition>> SettingsTypesAsync() =>
        (await definitions.ListTypeDefinitionsAsync()).Where(type => type.StereotypeEquals(Stereotype));

    private async Task<ContentTypeDefinition?> SettingsTypeAsync(string type)
    {
        var definition = await definitions.GetTypeDefinitionAsync(type);
        return definition is not null && definition.StereotypeEquals(Stereotype) ? definition : null;
    }

    private Task<bool> CanManageAsync(ContentTypeDefinition type) =>
        authorization.AuthorizeAsync(User, new Permission($"ManageCustomSettings_{type.Name}", $"Manage Custom Settings - {type.DisplayName}", [new Permission("ManageResourceSettings")]));
}
