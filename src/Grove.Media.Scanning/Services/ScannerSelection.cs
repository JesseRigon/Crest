namespace Grove.Media.Scanning.Services;

/// <summary>
/// Which scanners run for a tenant's upload (requirement 90): the host's, plus the tenant's
/// external scanner when it has one, or only the tenant's when it chose to bypass the host's.
/// A tenant with no external scanner always uses the host's.
/// </summary>
public static class ScannerSelection
{
    /// <summary>The name the tenant's own external scanner runs under.</summary>
    public const string TenantScannerName = "Tenant";

    public static IReadOnlyList<string> Select(IEnumerable<string> hostScanners, UploadScanningSettings? tenant)
    {
        ArgumentNullException.ThrowIfNull(hostScanners);

        var host = hostScanners
            .Where(name => !string.IsNullOrWhiteSpace(name) && !string.Equals(name, TenantScannerName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tenant is null || !tenant.HasExternalScanner)
        {
            return host;
        }

        return tenant.BypassHostScanners ? [TenantScannerName] : [.. host, TenantScannerName];
    }
}
