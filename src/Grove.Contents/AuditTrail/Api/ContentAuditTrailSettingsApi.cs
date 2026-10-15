#nullable enable

using Grove.Api;
using Grove.AuditTrail;
using Grove.Contents.Management.Metadata;
using Grove.Contents.AuditTrail.Settings;
using Grove.Entities;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Contents.AuditTrail.Api;

/// <summary>
/// The content section of the audit trail settings page (<c>api/grove/audit/settings/content</c>):
/// the content types whose events are not recorded; every other type is. Present while the
/// audit trail is enabled, with the audit trail settings permission.
/// </summary>
public sealed class ContentAuditTrailSettingsApi : ApiContributor
{
    public override string Name => "ContentAuditTrailSettings";

    public override string FeatureId => "Grove.Contents";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("audit/settings/content");
        routes.MapGet("", ApiHandlers.Handle<ContentAuditTrailSettingsHandlers>(handlers => handlers.GetAsync()));
        routes.MapPut("", (HttpContext __http, [FromBody] ContentAuditTrailSettingsWrite request) => ApiHandlers.For<ContentAuditTrailSettingsHandlers>(__http).SaveAsync(request));
    }
}

public sealed class ContentAuditTrailSettingsHandlers(
    IAuthorizationService authorization,
    ISiteService siteService,
    IContentDefinitionManager definitions) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, AuditTrailPermissions.ManageAuditTrailSettings)) return Forbid();

        return Ok(await ToModelAsync(await siteService.GetSettingsAsync<ContentAuditTrailSettings>()));
    }

    public async Task<IResult> SaveAsync([FromBody] ContentAuditTrailSettingsWrite request)
    {
        if (!await authorization.AuthorizeAsync(User, AuditTrailPermissions.ManageAuditTrailSettings)) return Forbid();

        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<ContentAuditTrailSettings>();
        settings.ExcludedContentTypes = [.. (request.ExcludedContentTypes ?? []).Where(type => !string.IsNullOrWhiteSpace(type)).Distinct(StringComparer.Ordinal)];
        site.Put(settings);
        await siteService.UpdateSiteSettingsAsync(site);

        return Ok(await ToModelAsync(settings));
    }

    private async Task<ContentAuditTrailSettingsModel> ToModelAsync(ContentAuditTrailSettings settings) =>
        new(settings.ExcludedContentTypes ?? [],
            [.. (await definitions.ListTypeDefinitionsAsync())
                .OrderBy(type => type.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(type => new ContentAuditTrailContentType(type.Name, type.DisplayName))]);
}

/// <summary>The excluded content types, and the content types to choose from.</summary>
public sealed record ContentAuditTrailSettingsModel(string[] ExcludedContentTypes, ContentAuditTrailContentType[] ContentTypes);

public sealed record ContentAuditTrailContentType(string Name, string DisplayName);

public sealed record ContentAuditTrailSettingsWrite(string[]? ExcludedContentTypes);
