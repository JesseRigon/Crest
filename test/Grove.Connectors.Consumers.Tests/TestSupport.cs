using System.Text.Json.Nodes;
using Grove.Display.Resources.Management;
using Grove.Entities;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace Grove.Connectors.Consumers.Tests;

public static class Sites
{
    /// <summary>A site service over one site settings document.</summary>
    public static ISiteService With(JsonObject properties)
    {
        var site = new FakeSite { Properties = properties };
        var siteService = Substitute.For<ISiteService>();
        siteService.GetSiteSettingsAsync().Returns(Task.FromResult<ISite>(site));
        siteService.LoadSiteSettingsAsync().Returns(Task.FromResult<ISite>(site));

        return siteService;
    }
}

/// <summary>The site settings: only <see cref="Properties"/> matters to the settings these tests read.</summary>
public sealed class FakeSite : ISite
{
    public JsonObject Properties { get; set; } = [];
    public string SiteName { get; set; } = "Test";
    public string PageTitleFormat { get; set; } = "";
    public string SiteSalt { get; set; } = "";
    public string SuperUser { get; set; } = "";
    public string Calendar { get; set; } = "";
    public string TimeZoneId { get; set; } = "";
    public ResourceDebugMode ResourceDebugMode { get; set; }
    public bool UseCdn { get; set; }
    public string CdnBaseUrl { get; set; } = "";
    public int PageSize { get; set; }
    public int MaxPageSize { get; set; }
    public bool AllowPageSizeSelection { get; set; }
    public int[] PageSizeOptions { get; set; } = [];
    public int MaxPagedCount { get; set; }
    public string BaseUrl { get; set; } = "";
    public RouteValueDictionary HomeRoute { get; set; } = [];
    public bool AppendVersion { get; set; }
    public CacheMode CacheMode { get; set; }

    public T As<T>() where T : new() => GetOrCreate<T>();

    public T GetOrCreate<T>() where T : new() => TryGet<T>(out var settings) ? settings : new T();

    public bool TryGet<T>(out T settings) => this.TryGet(typeof(T).Name, out settings);
}

public sealed class PlainLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
