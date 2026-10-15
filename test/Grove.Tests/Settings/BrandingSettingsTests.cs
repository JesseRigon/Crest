using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Entities;
using Grove.Data.Queries;
using Grove.Data.Queries.Builtin;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Tests.Settings;

// docs/branding.md › Verification (unit): the settings section round-trips, the site query
// resolves renditions to URLs and answers empty for an unset one, and the anonymous subset
// holds nothing but the name and the renditions.
public class BrandingSettingsTests
{
    private static BrandingOptions Options() => new BrandingOptions()
        .Add(new BrandingRendition("login", "Login logo", "login page"))
        .Add(new BrandingRendition("favicon", "Favicon", "document head"));

    [Fact]
    public void TheBrandingSectionRoundTrips()
    {
        var site = new SiteSettings();
        site.Alter<BrandingSettings>(settings => settings.Renditions["login"] = "_branding/login-1.png");

        var copy = JsonSerializer.Deserialize<SiteSettings>(JsonSerializer.Serialize(site, JOptions.Default), JOptions.Default);

        Assert.True(copy!.TryGet<BrandingSettings>(out var branding));
        Assert.Equal("_branding/login-1.png", branding.Renditions["login"]);
    }

    [Fact]
    public void RenditionsResolveToUrlsAndUnsetIsEmpty()
    {
        var site = new SiteSettings();
        site.Alter<BrandingSettings>(settings => settings.Renditions["login"] = "_branding/login-1.png");

        var urls = site.ResolveRenditions(Options(), new PrefixResolver());

        Assert.Equal("/media/_branding/login-1.png", urls["login"]);
        Assert.Equal(string.Empty, urls["favicon"]);
    }

    [Fact]
    public async Task TheAnonymousSiteRowIsTheNameAndTheRenditions()
    {
        var row = await SiteRowAsync(CallerContext.Anonymous("Default", CallerSide.Site));

        Assert.Equal(["Branding", "Name"], row.Select(pair => pair.Key).Order(StringComparer.Ordinal));
        Assert.Equal("Acme", row["Name"]!.GetValue<string>());
        Assert.Equal("/media/_branding/login-1.png", row["Branding"]!["login"]!.GetValue<string>());
        Assert.Equal(string.Empty, row["Branding"]!["favicon"]!.GetValue<string>());
    }

    [Fact]
    public async Task ASignedInCallerSeesTheRest()
    {
        var row = await SiteRowAsync(new CallerContext { Tenant = "Default", Side = CallerSide.Admin, UserId = "u1", UserName = "admin" });

        Assert.True(row.ContainsKey("BaseUrl"));
    }

    private static async Task<JsonObject> SiteRowAsync(CallerContext caller)
    {
        var site = new SiteSettings { SiteName = "Acme", BaseUrl = "https://acme.test" };
        site.Alter<BrandingSettings>(settings => settings.Renditions["login"] = "_branding/login-1.png");

        var services = new ServiceCollection()
            .AddSingleton<ISiteService>(new FixedSiteService(site))
            .AddSingleton<ICallerContextAccessor>(new FixedCaller(caller))
            .AddSingleton<IBrandingUrlResolver, PrefixResolver>()
            .AddSingleton(global::Microsoft.Extensions.Options.Options.Create(Options()))
            .BuildServiceProvider();

        var source = new SystemQuerySource(services);
        var results = await source.ExecuteQueryAsync(new Query { Name = SystemQuerySource.SiteQuery, Source = SystemQuerySource.SourceName }, new QueryRequest());

        return Assert.IsType<JsonObject>(Assert.Single(results.Items));
    }

    private sealed class PrefixResolver : IBrandingUrlResolver
    {
        public string ResolveUrl(string mediaPath) => "/media/" + mediaPath;
    }

    private sealed class FixedSiteService(ISite site) : ISiteService
    {
        public Task<ISite> LoadSiteSettingsAsync() => Task.FromResult(site);

        public Task<ISite> GetSiteSettingsAsync() => Task.FromResult(site);

        public Task UpdateSiteSettingsAsync(ISite site) => Task.CompletedTask;
    }

    private sealed class FixedCaller(CallerContext caller) : ICallerContextAccessor
    {
        public CallerContext Current { get; set; } = caller;
    }
}
