using Crest.Entities;
using Crest.FileStorage;
using Crest.Media;
using Crest.Services;
using Crest.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using CrestBrandingRendition = Crest.AdminTheme.Api.CrestBrandingRendition;
using CrestBrandingSettings = Crest.AdminTheme.Api.CrestBrandingSettings;

namespace Crest.Controllers;

/// <summary>
/// The branding group of the tenant's settings (docs/branding.md › Editing branding): one row
/// per registered rendition, its file uploaded into Media under a folder this service owns.
/// Settings writes need <c>ManageSettings</c>; a logo upload additionally passes Media's own
/// checks (<c>ManageMedia</c>, the allowed extensions, the upload pipeline). The display name
/// is edited with the general settings (<c>ISite.SiteName</c>).
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/site/branding-settings")]
public sealed class BrandingController(IRequestAccess requestAccess) : ControllerBase
{
    /// <summary>The media folder the branding files live in, relative to the tenant's media root.</summary>
    public const string MediaFolder = "_branding";

    private static readonly string[] s_imageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".ico"];

    [HttpGet]
    public async Task<ActionResult<CrestBrandingSettings>> GetAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, SettingsPermissions.ManageSettings);
        if (access is null) return Forbid();

        var site = await access.GetRequiredService<ISiteService>().GetSiteSettingsAsync();
        return Ok(Describe(access, site));
    }

    [HttpPost("{name}")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<CrestBrandingSettings>> UploadAsync(string name, IFormFile? file)
    {
        var access = await requestAccess.AuthorizeAsync(User, SettingsPermissions.ManageSettings);
        if (access is null || await requestAccess.AuthorizeAsync(User, MediaPermissions.ManageMedia) is null) return Forbid();
        if (Rendition(access, name) is not { } rendition) return NotFound();
        if (access.GetService<IMediaFileStore>() is not { } mediaStore) return NotFound("Media is not enabled for this tenant.");
        if (file is null || file.Length == 0) return BadRequest("A non-empty image file is required.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = rendition.Accept?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? s_imageExtensions;
        if (!allowed.Contains(extension, StringComparer.OrdinalIgnoreCase)) return BadRequest($"The {rendition.Label} must be one of: {string.Join(", ", allowed)}.");

        var mediaOptions = access.GetService<IOptions<MediaOptions>>()?.Value;
        if (mediaOptions?.AllowedFileExtensions is { Count: > 0 } mediaExtensions && !mediaExtensions.Contains(extension)) return BadRequest($"Media does not accept {extension} files.");

        await using var uploaded = file.OpenReadStream();
        await using var created = await access.GetRequiredService<FileCreationService>().CreateAsync(
            new FileCreatingContext(file.FileName, file.Length, file.ContentType),
            uploaded,
            HttpContext.RequestAborted);
        if (!created.Succeeded) return BadRequest(created.ErrorMessage ?? $"The uploaded file '{file.FileName}' was rejected.");

        // A fresh name per upload, so a changed logo is never served from a cache.
        var path = $"{MediaFolder}/{rendition.Name}-{Guid.NewGuid().ToString("n")[..8]}{extension}";
        await mediaStore.TryCreateDirectoryAsync(MediaFolder);
        await mediaStore.CreateFileFromStreamAsync(path, created.Stream, overwrite: true);

        var previous = await SetAsync(access, rendition.Name, path);
        await DeleteOwnedAsync(mediaStore, previous);

        return Ok(Describe(access, await access.GetRequiredService<ISiteService>().GetSiteSettingsAsync()));
    }

    [HttpDelete("{name}")]
    public async Task<ActionResult<CrestBrandingSettings>> ClearAsync(string name)
    {
        var access = await requestAccess.AuthorizeAsync(User, SettingsPermissions.ManageSettings);
        if (access is null) return Forbid();
        if (Rendition(access, name) is not { } rendition) return NotFound();

        var previous = await SetAsync(access, rendition.Name, null);
        if (access.GetService<IMediaFileStore>() is { } mediaStore && await requestAccess.AuthorizeAsync(User, MediaPermissions.ManageMedia) is not null)
        {
            await DeleteOwnedAsync(mediaStore, previous);
        }

        return Ok(Describe(access, await access.GetRequiredService<ISiteService>().GetSiteSettingsAsync()));
    }

    private static BrandingRendition? Rendition(AuthorizedRequest access, string name) =>
        access.GetRequiredService<IOptions<BrandingOptions>>().Value.Renditions
            .FirstOrDefault(rendition => string.Equals(rendition.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Sets (or, with null, clears) a rendition's media path; returns the path it replaced.</summary>
    private static async Task<string?> SetAsync(AuthorizedRequest access, string name, string? path)
    {
        var siteService = access.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();
        string? previous = null;

        site.Alter<BrandingSettings>(settings =>
        {
            previous = settings.Renditions.TryGetValue(name, out var existing) ? existing : null;
            if (path is null) settings.Renditions.Remove(name);
            else settings.Renditions[name] = path;
        });

        await siteService.UpdateSiteSettingsAsync(site);
        return previous;
    }

    // Only files in the folder this service owns are deleted; a path set some other way (a
    // recipe pointing at a library file) is left alone.
    private static async Task DeleteOwnedAsync(IMediaFileStore mediaStore, string? path)
    {
        if (!string.IsNullOrEmpty(path) && path.StartsWith(MediaFolder + "/", StringComparison.Ordinal))
        {
            await mediaStore.TryDeleteFileAsync(path);
        }
    }

    private static CrestBrandingSettings Describe(AuthorizedRequest access, ISite site)
    {
        var options = access.GetRequiredService<IOptions<BrandingOptions>>().Value;
        var urls = site.ResolveRenditions(options, access.GetService<IBrandingUrlResolver>());
        var stored = site.TryGet<BrandingSettings>(out var branding) ? branding.Renditions : new Dictionary<string, string>();

        return new CrestBrandingSettings(
            site.SiteName ?? string.Empty,
            options.Renditions.Select(rendition => new CrestBrandingRendition(
                rendition.Name,
                rendition.Label,
                rendition.UsedBy,
                rendition.Width,
                rendition.Height,
                rendition.Accept,
                rendition.Reserved,
                stored.TryGetValue(rendition.Name, out var path) ? path : null,
                urls.TryGetValue(rendition.Name, out var url) ? url : string.Empty)).ToArray());
    }
}
