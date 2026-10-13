using Crest.Media.Scanning;
using Crest.Media.Scanning.Services;
using Crest.Entities;
using Crest.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Crest.Tests.Media.Scanning;

// The tenant's external scanner secret is stored protected and handed out in clear; a save
// without a new secret keeps the stored one, and a secret written in clear still works.
public sealed class TenantScanningSettingsTests
{
    private SiteSettings _site = new();
    private readonly EphemeralDataProtectionProvider _protection = new();

    [Fact]
    public async Task TheSecretIsProtectedAtRestAndKeptWhenNotReplaced()
    {
        var settings = Service();

        await settings.UpdateAsync("https://scanner.example/jobs", "s3cret", bypassHostScanners: true);

        var stored = _site.As<UploadScanningSettings>();
        Assert.NotEqual("s3cret", stored.ExternalScannerSecret);
        Assert.Equal("s3cret", (await settings.GetAsync()).ExternalScannerSecret);

        await settings.UpdateAsync("https://scanner.example/v2", secret: null, bypassHostScanners: false);
        var current = await settings.GetAsync();
        Assert.Equal("s3cret", current.ExternalScannerSecret);
        Assert.Equal("https://scanner.example/v2", current.ExternalScannerUrl);
        Assert.False(current.BypassHostScanners);

        await settings.UpdateAsync(null, string.Empty, bypassHostScanners: false);
        Assert.Null((await settings.GetAsync()).ExternalScannerSecret);
    }

    [Fact]
    public async Task ASecretStoredInClearStillWorks()
    {
        _site.Alter<UploadScanningSettings>(value => value.ExternalScannerSecret = "plain");

        Assert.Equal("plain", (await Service().GetAsync()).ExternalScannerSecret);
    }

    private SiteTenantScanningSettings Service()
    {
        var sites = new Mock<ISiteService>();
        // As the site service does: a mutable copy to load, an immutable (cached) one to read,
        // and the saved copy becomes the next one read.
        sites.Setup(service => service.LoadSiteSettingsAsync()).ReturnsAsync(() => new SiteSettings { IsReadOnly = false, Properties = (System.Text.Json.Nodes.JsonObject)_site.Properties.DeepClone() });
        sites.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(() => _site);
        sites.Setup(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>())).Callback((ISite site) => _site = new SiteSettings { Properties = (System.Text.Json.Nodes.JsonObject)site.Properties.DeepClone() }).Returns(Task.CompletedTask);
        return new SiteTenantScanningSettings(sites.Object, _protection, NullLogger<SiteTenantScanningSettings>.Instance);
    }
}
