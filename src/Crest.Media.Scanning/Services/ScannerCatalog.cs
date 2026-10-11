using Crest.Media.Files;
using Crest.Media.Scanning.Scanners;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Media.Scanning.Services;

/// <summary>
/// Resolves scanners by name: providers registered in the container (ClamAV), the host's offsite
/// scanners from configuration, and the tenant's external scanner from its settings. Scanners are
/// host-level and shared; each job carries only its own tenant's data (requirement 89).
/// </summary>
public sealed class ScannerCatalog(
    IEnumerable<IUploadScanner> registered,
    IOptions<UploadScanningOptions> options,
    ITenantScanningSettings tenantSettings,
    IServiceProvider serviceProvider)
{
    private readonly UploadScanningOptions _options = options.Value;

    /// <summary>The scanners for a new upload, chosen once at submission.</summary>
    public async Task<IReadOnlyList<string>> SelectAsync()
        => ScannerSelection.Select(_options.Scanners, await tenantSettings.GetAsync());

    public async Task<IUploadScanner?> FindAsync(string name)
    {
        if (string.Equals(name, ScannerSelection.TenantScannerName, StringComparison.OrdinalIgnoreCase))
        {
            var settings = await tenantSettings.GetAsync();

            return settings.HasExternalScanner
                ? ActivatorUtilities.CreateInstance<OffsiteUploadScanner>(serviceProvider, ScannerSelection.TenantScannerName, new OffsiteScannerOptions
                {
                    Url = settings.ExternalScannerUrl,
                    Secret = settings.ExternalScannerSecret,
                })
                : null;
        }

        if (_options.Offsite.TryGetValue(name, out var offsite))
        {
            return ActivatorUtilities.CreateInstance<OffsiteUploadScanner>(serviceProvider, name, offsite);
        }

        return registered.FirstOrDefault(scanner => string.Equals(scanner.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The shared secret a scanner's callbacks are signed with; null for a scanner that cannot call back.</summary>
    public async Task<string?> GetCallbackSecretAsync(string name)
    {
        if (string.Equals(name, ScannerSelection.TenantScannerName, StringComparison.OrdinalIgnoreCase))
        {
            var settings = await tenantSettings.GetAsync();

            return settings.HasExternalScanner ? NullIfEmpty(settings.ExternalScannerSecret) : null;
        }

        return _options.Offsite.TryGetValue(name, out var offsite) ? NullIfEmpty(offsite.Secret) : null;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
