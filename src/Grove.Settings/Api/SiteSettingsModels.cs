#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Grove.ContentManagement.Routing;
using Grove.Settings;

namespace Grove.Settings.Api;

public sealed record SiteSettingsModel(
    string SiteName,
    string PageTitleFormat,
    string BaseUrl,
    string TimeZoneId,
    string Calendar,
    int PageSize,
    int MaxPageSize,
    int MaxPagedCount,
    bool AppendVersion,
    bool UseCdn,
    string CdnBaseUrl,
    string ResourceDebugMode,
    string CacheMode)
{
    public static SiteSettingsModel From(ISite site) => new(
        site.SiteName,
        site.PageTitleFormat,
        site.BaseUrl,
        site.TimeZoneId,
        site.Calendar,
        site.PageSize,
        site.MaxPageSize,
        site.MaxPagedCount,
        site.AppendVersion,
        site.UseCdn,
        site.CdnBaseUrl,
        site.ResourceDebugMode.ToString(),
        site.CacheMode.ToString());
}

public sealed record SiteSettingsUpdate(
    string SiteName,
    string PageTitleFormat,
    string BaseUrl,
    string TimeZoneId,
    string Calendar,
    int PageSize,
    int MaxPageSize,
    int MaxPagedCount,
    bool AppendVersion,
    bool UseCdn,
    string CdnBaseUrl,
    string ResourceDebugMode,
    string CacheMode);

public sealed record SiteHomeResult(string ContentItemId);


/// <summary>
/// The tenant's public identity, readable anonymously (the login page greets with it): only
/// public-safe facts, nothing else from <c>ISite</c>. Logo slots join it later
/// (docs/branding.md).
/// </summary>
public sealed record SiteBrandingResult(string SiteName, IReadOnlyDictionary<string, string>? Renditions = null);
