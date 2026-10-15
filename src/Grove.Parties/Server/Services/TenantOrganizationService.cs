using Grove.Contents.Management;
using Grove.Entities;
using Grove.Parties.Constants;
using Grove.Infrastructure.Settings;

namespace Grove.Parties.Services;

/// <summary>
/// The site-settings reference to the Organization party that represents the tenant's
/// legal entity: its legal names, tax id and addresses come from there, while the display
/// name stays <see cref="ISite.SiteName"/>. A reference, never a flag on the item.
/// </summary>
public sealed class TenantOrganizationSettings
{
    public string? OrganizationId { get; set; }
}

/// <summary>
/// The tenant's own organization (ruling 2026-10-05). It exists before the first user,
/// created during tenant setup, because staff belong to it. Members asks
/// <see cref="IsTenantOrganizationAsync"/> before any binding write, so whether members
/// may be bound to it is decided in one place (a Members tenant setting, default blocked).
/// </summary>
public interface ITenantOrganizationService
{
    /// <summary>The tenant organization's content item id; null only before setup created it.</summary>
    Task<string?> GetIdAsync(CancellationToken cancellationToken = default);

    /// <summary>"Is this the tenant's organization", read from the site-settings reference.</summary>
    Task<bool> IsTenantOrganizationAsync(string? organizationId, CancellationToken cancellationToken = default);

    /// <summary>Creates the organization (titled with the site name) and records the
    /// reference when none exists or the referenced item is gone; returns its id. Idempotent.</summary>
    Task<string> EnsureAsync(CancellationToken cancellationToken = default);
}

public sealed class TenantOrganizationService(ISiteService siteService, IContentManager contentManager) : ITenantOrganizationService
{
    public async Task<string?> GetIdAsync(CancellationToken cancellationToken = default)
    {
        var settings = await siteService.GetSettingsAsync<TenantOrganizationSettings>();
        return string.IsNullOrWhiteSpace(settings.OrganizationId) ? null : settings.OrganizationId;
    }

    public async Task<bool> IsTenantOrganizationAsync(string? organizationId, CancellationToken cancellationToken = default) =>
        !string.IsNullOrWhiteSpace(organizationId)
        && string.Equals(await GetIdAsync(cancellationToken), organizationId, StringComparison.Ordinal);

    public async Task<string> EnsureAsync(CancellationToken cancellationToken = default)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<TenantOrganizationSettings>();
        if (!string.IsNullOrWhiteSpace(settings.OrganizationId)
            && await contentManager.GetAsync(settings.OrganizationId, VersionOptions.Latest) is { ContentType: PartiesConstants.ContentTypes.Organization })
        {
            return settings.OrganizationId;
        }

        var organization = await contentManager.NewAsync(PartiesConstants.ContentTypes.Organization);
        organization.DisplayText = string.IsNullOrWhiteSpace(site.SiteName) ? "Our organization" : site.SiteName;
        ((System.Text.Json.Nodes.JsonObject)organization.Content)["TitlePart"] = new System.Text.Json.Nodes.JsonObject { ["Title"] = organization.DisplayText };
        await contentManager.CreateAsync(organization, VersionOptions.Published);

        settings.OrganizationId = organization.ContentItemId;
        site.Put(settings);
        await siteService.UpdateSiteSettingsAsync(site);
        return organization.ContentItemId;
    }
}
