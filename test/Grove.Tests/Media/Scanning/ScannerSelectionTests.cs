#nullable enable

using Grove.Media.Files;
using Grove.Media.Scanning;
using Grove.Media.Scanning.Services;

namespace Grove.Tests.Media.Scanning;

/// <summary>Requirement 90: the host's scanners run for every tenant unless a tenant with its own scanner bypasses them.</summary>
public class ScannerSelectionTests
{
    private static readonly string[] s_host = ["ClamAV", "Cloud"];

    [Fact]
    public void ATenantWithoutItsOwnScannerUsesTheHosts()
        => Assert.Equal(s_host, ScannerSelection.Select(s_host, new UploadScanningSettings()));

    [Fact]
    public void ATenantScannerRunsAlongsideTheHostsByDefault()
    {
        var tenant = new UploadScanningSettings { ExternalScannerUrl = "https://scan.example.com/jobs", ExternalScannerSecret = "s" };

        Assert.Equal(["ClamAV", "Cloud", ScannerSelection.TenantScannerName], ScannerSelection.Select(s_host, tenant));
    }

    [Fact]
    public void ATenantScannerCanBypassTheHosts()
    {
        var tenant = new UploadScanningSettings { ExternalScannerUrl = "https://scan.example.com/jobs", BypassHostScanners = true };

        Assert.Equal([ScannerSelection.TenantScannerName], ScannerSelection.Select(s_host, tenant));
    }

    [Fact]
    public void BypassingWithoutAnOwnScannerStillUsesTheHosts()
        => Assert.Equal(s_host, ScannerSelection.Select(s_host, new UploadScanningSettings { BypassHostScanners = true }));

    [Fact]
    public void TheHostCannotNameTheTenantScannerOrRepeatOne()
        => Assert.Equal(["ClamAV"], ScannerSelection.Select(["ClamAV", "clamav", "Tenant", " "], null));

    [Fact]
    public async Task TheCatalogFindsHostScannersAndTheTenantsOwn()
    {
        var world = new ScanningWorld();
        world.AddScanner("ClamAV", ScanVerdict.Clean);
        world.Options.Offsite["Cloud"] = new OffsiteScannerOptions { Url = "https://cloud.example.com/scan", Secret = "host-secret" };
        world.TenantSettings = new UploadScanningSettings { ExternalScannerUrl = "https://tenant.example.com/scan", ExternalScannerSecret = "tenant-secret" };

        var services = new ServiceCollection()
            .AddHttpClient()
            .AddSingleton(new global::Grove.Environment.Shell.ShellSettings { Name = "Default" })
            .AddSingleton(global::Microsoft.Extensions.Options.Options.Create(world.Options))
            .AddLogging()
            .BuildServiceProvider();
        var catalog = world.Catalog(services);

        Assert.Equal(["ClamAV", ScannerSelection.TenantScannerName], await catalog.SelectAsync());
        Assert.Equal("ClamAV", (await catalog.FindAsync("clamav"))?.Name);
        Assert.Equal("Cloud", (await catalog.FindAsync("Cloud"))?.Name);
        Assert.Equal(ScannerSelection.TenantScannerName, (await catalog.FindAsync("Tenant"))?.Name);
        Assert.Null(await catalog.FindAsync("Unknown"));

        Assert.Equal("host-secret", await catalog.GetCallbackSecretAsync("Cloud"));
        Assert.Equal("tenant-secret", await catalog.GetCallbackSecretAsync("Tenant"));
        Assert.Null(await catalog.GetCallbackSecretAsync("ClamAV"));
    }

    [Fact]
    public async Task TheTenantSecretIsGoneWithItsScanner()
    {
        var world = new ScanningWorld { TenantSettings = new UploadScanningSettings { ExternalScannerSecret = "left-over" } };
        var catalog = world.Catalog(new ServiceCollection().BuildServiceProvider());

        Assert.Null(await catalog.GetCallbackSecretAsync("Tenant"));
        Assert.Null(await catalog.FindAsync("Tenant"));
    }
}
