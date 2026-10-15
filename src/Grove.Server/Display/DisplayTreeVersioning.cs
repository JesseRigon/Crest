using System.Text.Json;
using Grove.ContentManagement.Handlers;
using Grove.DisplayManagement.Components;
using Grove.Settings;

namespace Grove.Display;

/// <summary>
/// Bumps the content part of every tree's composite version (decision 12) when content any tree
/// may show changes: a page's item, a menu, a list's children, a template. Coarse on purpose: a
/// change costs a rebuild, never a stale page.
/// </summary>
public sealed class DisplayTreeContentHandler(DisplayTreeVersions versions) : ContentHandlerBase
{
    public override Task PublishedAsync(PublishContentContext context) => Bump();

    public override Task UnpublishedAsync(PublishContentContext context) => Bump();

    public override Task RemovedAsync(RemoveContentContext context) => Bump();

    public override Task UpdatedAsync(UpdateContentContext context) => Bump();

    public override Task ImportedAsync(ImportContentContext context) => Bump();

    private Task Bump()
    {
        versions.BumpContent();
        return Task.CompletedTask;
    }
}

/// <summary>
/// The site settings' part of a tree's composite version (decision 12): the site name and home
/// route the layout shows, and the settings a page is built under (display settings, theme).
/// </summary>
public sealed class SiteTreeVersion(ISiteService siteService) : IDisplayTreeVersionPart
{
    public async ValueTask<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var site = await siteService.GetSiteSettingsAsync();
        return string.Join('\u001f', site.SiteName, JsonSerializer.Serialize(site.HomeRoute), site.Properties?.ToJsonString());
    }
}
