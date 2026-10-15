using System.Security.Cryptography;
using Grove.Entities;
using Grove.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Grove.Media.Scanning.Services;

/// <summary>The tenant's scanning choice, from its site settings.</summary>
public interface ITenantScanningSettings
{
    /// <summary>The settings in use, the external scanner's secret in clear.</summary>
    Task<UploadScanningSettings> GetAsync();

    /// <summary>
    /// Saves the tenant's choice. <paramref name="secret"/> null keeps the stored secret, empty
    /// removes it; a new one is stored protected (data protection), never in clear.
    /// </summary>
    Task UpdateAsync(string? externalScannerUrl, string? secret, bool bypassHostScanners);
}

public sealed class SiteTenantScanningSettings(
    ISiteService siteService,
    IDataProtectionProvider dataProtection,
    ILogger<SiteTenantScanningSettings> logger) : ITenantScanningSettings
{
    private const string Purpose = "Grove.Media.Scanning.ExternalScannerSecret";

    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);

    public async Task<UploadScanningSettings> GetAsync()
    {
        var stored = await siteService.GetSettingsAsync<UploadScanningSettings>();

        return new UploadScanningSettings
        {
            ExternalScannerUrl = stored.ExternalScannerUrl,
            ExternalScannerSecret = Unprotect(stored.ExternalScannerSecret),
            BypassHostScanners = stored.BypassHostScanners,
        };
    }

    public async Task UpdateAsync(string? externalScannerUrl, string? secret, bool bypassHostScanners)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        site.Alter<UploadScanningSettings>(settings =>
        {
            settings.ExternalScannerUrl = string.IsNullOrWhiteSpace(externalScannerUrl) ? null : externalScannerUrl.Trim();
            settings.BypassHostScanners = bypassHostScanners;

            if (secret is not null)
            {
                settings.ExternalScannerSecret = secret.Length == 0 ? null : _protector.Protect(secret);
            }
        });

        await siteService.UpdateSiteSettingsAsync(site);
    }

    // A secret written before the editor existed (a recipe, hand-edited settings) is in clear:
    // it still works, and the next save through the editor protects it.
    private string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(stored);
        }
        catch (CryptographicException)
        {
            logger.LogWarning("The tenant's external scanner secret is stored unprotected; save the scanning settings to protect it.");
            return stored;
        }
    }
}
