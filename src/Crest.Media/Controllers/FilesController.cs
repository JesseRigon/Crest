#nullable enable
using Crest.Access;
using Crest.Media.Files;
using DriveType = Crest.Media.Files.DriveType;
using Crest.Media.Sharing;
using Crest.Sharing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Media.Controllers;

/// <summary>
/// The file-object API over HTTP (docs/media.md § 3): drives, folders, files, links and
/// versions, every call as the gated caller through <see cref="IMediaObjectService"/>. What the
/// caller cannot view answers 404, whether or not it exists (requirement 68).
/// </summary>
[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[Route("api/files")]
public sealed class FilesController(
    IMediaObjectService files,
    IMediaObjectListing listing,
    IMediaDriveAdministration drives,
    IPersonalDriveAccessService personalAccess,
    IFileDriveSettingsAccessor settings,
    IAccessDecision decision,
    ICallerContextAccessor callers,
    HmacMediaUrlSigner signer) : ControllerBase
{
    private const int MaxPageSize = 500;
    private static readonly TimeSpan PrivateUrlLifetime = TimeSpan.FromMinutes(5);

    [HttpGet("drives")]
    public Task<IActionResult> ListDrivesAsync() => RunAsync(async () =>
        Ok(await ToDtosAsync(await files.ListDrivesAsync(HttpContext.RequestAborted))));

    [HttpPost("drives")]
    public Task<IActionResult> CreateDriveAsync([FromBody] CreateDriveRequest request) => RunAsync(async () =>
    {
        var owner = request.Type switch
        {
            DriveType.Shared => MediaOwner.Tenant,
            DriveType.Organization => MediaOwner.Organization(request.OwnerId ?? string.Empty),
            _ => MediaOwner.User(request.OwnerId ?? callers.Current?.UserId ?? string.Empty),
        };
        return Ok(await ToDtoAsync(await files.CreateDriveAsync(request.Name, request.Type, owner, HttpContext.RequestAborted)));
    });

    [HttpGet("{id}")]
    public Task<IActionResult> GetAsync(string id) => RunAsync(async () =>
        await files.GetAsync(id, HttpContext.RequestAborted) is { } item ? Ok(await ToDtoAsync(item)) : NotFound());

    /// <summary>The children the caller may view, paged after filtering.</summary>
    [HttpGet("{id}/children")]
    public Task<IActionResult> ListChildrenAsync(string id, [FromQuery] int skip = 0, [FromQuery] int take = 100) => RunAsync(async () =>
    {
        // Filtered and paged in the query while sharing is on (requirement 77).
        take = Math.Clamp(take, 1, MaxPageSize);
        var page = await listing.ListChildrenPageAsync(id, skip, take, HttpContext.RequestAborted);
        return Ok(new FileListDto(await ToDtosAsync(page.Items), page.Total, skip, take));
    });

    [HttpPost("{id}/folders")]
    public Task<IActionResult> CreateFolderAsync(string id, [FromBody] NameRequest request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.CreateFolderAsync(id, request.Name, HttpContext.RequestAborted))));

    [HttpPost("{id}/files")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public Task<IActionResult> UploadAsync(string id, IFormFile file, [FromForm] string? name = null) => RunAsync(async () =>
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        await using var stream = file.OpenReadStream();
        var item = await files.UploadAsync(new MediaUpload(id, name ?? file.FileName, stream, file.ContentType), HttpContext.RequestAborted);
        return Ok(await ToDtoAsync(item));
    });

    [HttpPut("{id}/content")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public Task<IActionResult> ReplaceAsync(string id, IFormFile file) => RunAsync(async () =>
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        await using var stream = file.OpenReadStream();
        return Ok(await ToDtoAsync(await files.ReplaceAsync(id, stream, file.ContentType, HttpContext.RequestAborted)));
    });

    [HttpPost("{id}/rename")]
    public Task<IActionResult> RenameAsync(string id, [FromBody] NameRequest request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.RenameAsync(id, request.Name, HttpContext.RequestAborted))));

    [HttpPost("{id}/move")]
    public Task<IActionResult> MoveAsync(string id, [FromBody] PlaceRequest request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.MoveAsync(id, request.ParentId, HttpContext.RequestAborted))));

    [HttpPost("{id}/link")]
    public Task<IActionResult> LinkAsync(string id, [FromBody] PlaceRequest request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.LinkAsync(id, request.ParentId, request.Name, HttpContext.RequestAborted))));

    [HttpPost("{id}/shortcut")]
    public Task<IActionResult> ShortcutAsync(string id, [FromBody] PlaceRequest request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.ShortcutAsync(id, request.ParentId, request.Name, HttpContext.RequestAborted))));

    [HttpDelete("{id}")]
    public Task<IActionResult> DeleteAsync(string id) => RunAsync(async () =>
    {
        await files.DeleteAsync(id, HttpContext.RequestAborted);
        return NoContent();
    });

    [HttpPut("{id}/metadata")]
    public Task<IActionResult> UpdateMetadataAsync(string id, [FromBody] MetadataDto request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.UpdateMetadataAsync(id, new MediaMetadata(request.Title, request.Description, request.AlternativeText, request.Tags), HttpContext.RequestAborted))));

    [HttpGet("{id}/versions")]
    public Task<IActionResult> ListVersionsAsync(string id) => RunAsync(async () =>
        await files.GetAsync(id, HttpContext.RequestAborted) is { } item ? Ok((await ToDtoAsync(item)).Versions) : NotFound());

    [HttpPost("{id}/versions/{version:int}/publish")]
    public Task<IActionResult> PublishAsync(string id, int version) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.PublishVersionAsync(id, version, HttpContext.RequestAborted))));

    [HttpGet("{id}/download")]
    public Task<IActionResult> DownloadAsync(string id, [FromQuery] int? version = null) => RunAsync(async () =>
    {
        var item = await files.GetAsync(id, HttpContext.RequestAborted);
        var stream = item is null ? null : await files.OpenReadAsync(id, version, HttpContext.RequestAborted);
        if (item is null || stream is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, "application/octet-stream", item.Placement.Name);
    });

    /// <summary>The URL the bytes are served at for this caller: the plain one for a public
    /// file, a short-lived signed one otherwise (docs/media.md › Serving and CDNs).</summary>
    [HttpGet("{id}/url")]
    public Task<IActionResult> UrlAsync(string id, [FromQuery] int? version = null) => RunAsync(async () =>
    {
        if (await files.GetAsync(id, HttpContext.RequestAborted) is not { } item)
        {
            return NotFound();
        }

        var path = Request.PathBase + FileServingEndpoint.PathFor(item.Placement.Id, version);
        if (await IsPublicAsync(item))
        {
            return Ok(new FileUrlDto(path, null));
        }

        var expires = DateTimeOffset.UtcNow.Add(PrivateUrlLifetime);
        return Ok(new FileUrlDto(signer.Sign(new Uri(path, UriKind.Relative), expires).OriginalString, expires));
    });

    // ---- Tenant administration (docs/media.md § 3, § 7) -----------------------------------

    [HttpGet("settings")]
    public Task<IActionResult> GetSettingsAsync() => RunAsync(async () =>
    {
        RequireTenantAdministrator();
        return Ok(new FileSettingsDto(await settings.GetAsync(), await settings.GetPersonalDriveAccessAsync()));
    });

    /// <summary>Saves the settings; a drive type turned off makes its drives inactive, turned
    /// on makes them active again, keeping their sharing or resetting it (requirements 21-22).</summary>
    [HttpPut("settings")]
    public Task<IActionResult> SaveSettingsAsync([FromBody] SaveFileSettingsRequest request) => RunAsync(async () =>
    {
        RequireTenantAdministrator();
        var before = await settings.GetAsync();
        if (request.PersonalDriveAccess is { } personal && personal.Delay > TimeSpan.Zero && personal.Notice == PersonalDriveNotice.None)
        {
            return BadRequest("A notice delay needs a notice.");
        }

        await settings.SaveAsync(request.Drives, request.PersonalDriveAccess);
        if (request.Drives is { } after)
        {
            foreach (var type in new[] { DriveType.Organization, DriveType.Personal })
            {
                if (before.Allows(type) != after.Allows(type))
                {
                    await drives.SetDriveTypeEnabledAsync(type, after.Allows(type), request.ResetSharingOnReactivation, HttpContext.RequestAborted);
                }
            }
        }

        return NoContent();
    });

    [HttpPost("drives/{id}/inactive")]
    public Task<IActionResult> SetInactiveAsync(string id, [FromBody] InactiveRequest request) => RunAsync(async () =>
    {
        await drives.SetDriveInactiveAsync(id, request.Inactive, request.ResetSharing, HttpContext.RequestAborted);
        return NoContent();
    });

    /// <summary>The separate "open a user's drive" step (requirement 24): finds the drive, never
    /// what is in it.</summary>
    [HttpGet("personal-drives/{userId}")]
    public Task<IActionResult> FindPersonalDriveAsync(string userId) => RunAsync(async () =>
        await drives.FindPersonalDriveAsync(userId, HttpContext.RequestAborted) is { } drive
            ? Ok(new PersonalDriveDto(drive.Placement.Id, drive.Placement.Name, drive.Content.Owner.Id, drive.Content.IsInactive, await personalAccess.CurrentAsync(drive.Placement.Id, HttpContext.RequestAborted), (await settings.GetPersonalDriveAccessAsync()).Durations))
            : NotFound());

    [HttpPost("personal-drives/{driveId}/access")]
    public Task<IActionResult> RequestAccessAsync(string driveId, [FromBody] PersonalDriveAccessRequest request) => RunAsync(async () =>
        Ok(await personalAccess.RequestAsync(driveId, request.UntilClosed ? null : request.Duration, request.OverrideDelay, request.Reason, HttpContext.RequestAborted)));

    [HttpGet("personal-drives/{driveId}/access")]
    public Task<IActionResult> CurrentAccessAsync(string driveId) => RunAsync(async () =>
        await personalAccess.CurrentAsync(driveId, HttpContext.RequestAborted) is { } access ? Ok(access) : NotFound());

    [HttpDelete("personal-drives/access/{accessId}")]
    public Task<IActionResult> CloseAccessAsync(string accessId) => RunAsync(async () =>
    {
        await personalAccess.CloseAsync(accessId, HttpContext.RequestAborted);
        return NoContent();
    });

    /// <summary>What may happen to a deleted user's personal drive (requirement 20).</summary>
    [HttpGet("personal-drives/{userId}/disposal")]
    public Task<IActionResult> DisposalChoicesAsync(string userId) => RunAsync(() =>
    {
        RequireTenantAdministrator();
        return Task.FromResult<IActionResult>(Ok(drives.DisposalChoices));
    });

    [HttpPost("personal-drives/{userId}/disposal")]
    public Task<IActionResult> DisposeAsync(string userId, [FromBody] DisposalRequest request) => RunAsync(async () =>
    {
        await drives.DisposePersonalDriveAsync(userId, request.Choice, request.TargetId, HttpContext.RequestAborted);
        return NoContent();
    });

    private void RequireTenantAdministrator()
    {
        if (callers.Current is not { IsSuperUser: true })
        {
            throw new AccessDeniedException(MediaPermissions.ManageSharedDrives.Name, "Only a tenant administrator changes the file settings.");
        }
    }

    private async Task<bool> IsPublicAsync(MediaItem item)
    {
        if (HttpContext.RequestServices.GetService<ISharingResolver>() is not { } resolver || callers.Current is not { } caller)
        {
            return false;
        }

        var resolution = await resolver.ResolveAsync(caller, new ShareableRef(MediaFileShareableKind.KindName, item.Placement.LinkTargetPlacementId ?? item.Placement.Id), HttpContext.RequestAborted);
        return resolution.IsPublic && !resolution.IsInactive;
    }

    private async Task<IActionResult> RunAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (MediaObjectNotFoundException)
        {
            return NotFound();
        }
        catch (AccessDeniedException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, exception.Message);
        }
        catch (MediaObjectConflictException exception)
        {
            return Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    private async Task<List<FileItemDto>> ToDtosAsync(IEnumerable<MediaItem> items)
    {
        var dtos = new List<FileItemDto>();
        foreach (var item in items)
        {
            dtos.Add(await ToDtoAsync(item));
        }

        return dtos;
    }

    /// <summary>Readers get the published version only; drafts and their states are for
    /// Editors and Managers.</summary>
    private async Task<FileItemDto> ToDtoAsync(MediaItem item)
    {
        var content = item.Content;
        var canEdit = await decision.AllowsAsync(callers.Current, MediaPermissions.EditMedia.Name, item.Placement, HttpContext.RequestAborted);
        var versions = content.Versions
            .Where(version => canEdit || version.Number == content.PublishedVersion)
            .OrderByDescending(version => version.Number)
            .Select(version => new FileVersionDto(version.Number, version.Size, version.DetectedType ?? version.ClaimedType, version.State, version.Reason, version.CreatedUtc, version.CreatedBy, version.Number == content.PublishedVersion))
            .ToList();
        var published = content.Versions.FirstOrDefault(version => version.Number == content.PublishedVersion);

        return new FileItemDto(
            item.Placement.Id,
            content.Id,
            item.Placement.ParentId,
            item.Placement.DriveId,
            item.Placement.Name,
            content.Kind,
            item.Placement.LinkTargetPlacementId,
            content.DriveType,
            content.IsInactive,
            new MetadataDto(content.Metadata.Title, content.Metadata.Description, content.Metadata.AlternativeText, content.Metadata.Tags),
            published?.Size,
            published?.DetectedType ?? published?.ClaimedType,
            content.PublishedVersion,
            canEdit,
            content.CreatedUtc,
            content.CreatedBy,
            content.ModifiedUtc,
            content.ModifiedBy,
            versions);
    }
}

public sealed record CreateDriveRequest(string Name, DriveType Type, string? OwnerId = null);

public sealed record NameRequest(string Name);

public sealed record PlaceRequest(string ParentId, string? Name = null);

public sealed record MetadataDto(string? Title, string? Description, string? AlternativeText, IReadOnlyList<string>? Tags);

public sealed record FileVersionDto(int Number, long Size, string? Type, MediaVersionState State, string? Reason, DateTimeOffset CreatedUtc, string? CreatedBy, bool IsPublished);

public sealed record FileItemDto(
    string Id,
    string ContentId,
    string? ParentId,
    string DriveId,
    string Name,
    MediaObjectKind Kind,
    string? ShortcutTargetId,
    DriveType? DriveType,
    bool IsInactive,
    MetadataDto Metadata,
    long? Size,
    string? Type,
    int? PublishedVersion,
    bool CanEdit,
    DateTimeOffset CreatedUtc,
    string? CreatedBy,
    DateTimeOffset ModifiedUtc,
    string? ModifiedBy,
    IReadOnlyList<FileVersionDto> Versions);

public sealed record FileListDto(IReadOnlyList<FileItemDto> Items, int Total, int Skip, int Take);

public sealed record FileUrlDto(string Url, DateTimeOffset? Expires);

public sealed record FileSettingsDto(FileDriveSettings Drives, PersonalDriveAccessSettings PersonalDriveAccess);

public sealed record SaveFileSettingsRequest(FileDriveSettings? Drives, PersonalDriveAccessSettings? PersonalDriveAccess, bool ResetSharingOnReactivation = false);

public sealed record InactiveRequest(bool Inactive, bool ResetSharing = false);

public sealed record PersonalDriveAccessRequest(TimeSpan? Duration, bool UntilClosed = false, bool OverrideDelay = false, string? Reason = null);

public sealed record PersonalDriveDto(string DriveId, string Name, string? OwnerUserId, bool IsInactive, PersonalDriveAccess? CurrentAccess, IReadOnlyList<TimeSpan?> Durations);

public sealed record DisposalRequest(PersonalDriveDisposal Choice, string? TargetId = null);
