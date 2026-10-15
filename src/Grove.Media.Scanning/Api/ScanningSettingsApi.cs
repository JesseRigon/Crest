using Grove.Api;
using Microsoft.AspNetCore.Http;
using Grove.Media.Scanning.Services;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Media.Scanning.Api;

/// <summary>The tenant's upload scanning settings as the admin editor reads and writes them.</summary>
/// <param name="ExternalScannerUrl">Where the tenant's own scanner accepts jobs; empty for none.</param>
/// <param name="HasSecret">Whether a secret is stored; the secret itself never leaves the server.</param>
/// <param name="BypassHostScanners">Only the tenant's scanner runs.</param>
public sealed record ScanningSettingsModel(string? ExternalScannerUrl, bool HasSecret, bool BypassHostScanners);

/// <param name="Secret">A new secret; null keeps the stored one.</param>
/// <param name="ClearSecret">Removes the stored secret.</param>
public sealed record ScanningSettingsUpdate(string? ExternalScannerUrl, string? Secret, bool ClearSecret, bool BypassHostScanners);

/// <summary>
/// <c>api/grove/media/scanning/settings</c>: the tenant's upload scanning settings (its own
/// external scanner and secret), for the admin editor. Needs <c>ManageSettings</c>.
/// </summary>
public sealed class ScanningSettingsApi : ApiContributor
{
    public override string Name => "ScanningSettings";

    public override string FeatureId => "Grove.Media.Scanning";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("media/scanning/settings");
        routes.MapGet("", ApiHandlers.Handle<ScanningSettingsHandlers>(h => h.GetAsync()));
        routes.MapPut("", (HttpContext http, [FromBody] ScanningSettingsUpdate request) => ApiHandlers.For<ScanningSettingsHandlers>(http).SaveAsync(request));
    }
}

public sealed class ScanningSettingsHandlers(ITenantScanningSettings settings, IAuthorizationService authorization) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, SettingsPermissions.ManageSettings))
        {
            return Forbid();
        }

        return Ok(ToModel(await settings.GetAsync()));
    }

    public async Task<IResult> SaveAsync([FromBody] ScanningSettingsUpdate request)
    {
        if (!await authorization.AuthorizeAsync(User, SettingsPermissions.ManageSettings))
        {
            return Forbid();
        }

        var url = request.ExternalScannerUrl?.Trim();
        if (!string.IsNullOrEmpty(url)
            && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
        {
            return BadRequest("The external scanner address must be an absolute http or https URL.");
        }

        var secret = request.ClearSecret ? string.Empty : string.IsNullOrEmpty(request.Secret) ? null : request.Secret;
        var current = await settings.GetAsync();
        var willHaveSecret = secret is null ? !string.IsNullOrEmpty(current.ExternalScannerSecret) : secret.Length > 0;
        if (!string.IsNullOrEmpty(url) && !willHaveSecret)
        {
            return BadRequest("An external scanner needs a secret: jobs and verdicts are signed with it.");
        }

        await settings.UpdateAsync(url, secret, request.BypassHostScanners);
        return Ok(ToModel(await settings.GetAsync()));
    }

    private static ScanningSettingsModel ToModel(UploadScanningSettings value) =>
        new(value.ExternalScannerUrl, !string.IsNullOrEmpty(value.ExternalScannerSecret), value.BypassHostScanners);
}
