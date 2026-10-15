#nullable enable

using Grove.Api;
using Grove.Access;
using Grove.Media.Files;
using DriveType = Grove.Media.Files.DriveType;
using Grove.Media.Sharing;
using Grove.Access.Sharing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Media.Api;

/// <summary>
/// <c>api/files</c>: the file-object API over HTTP (docs/media.md § 3): drives, folders, files,
/// links and versions, every call as the gated caller through <see cref="IMediaObjectService"/>.
/// What the caller cannot view answers 404, whether or not it exists (requirement 68). Used by
/// the native file pages (Grove.Display.Components' <c>GroveFilesClient</c>).
/// </summary>
public sealed class FilesApi : ApiContributor
{
    private const long MaxUploadBytes = 100 * 1024 * 1024;

    public override string Name => "Files";

    public override string FeatureId => "Grove.Media";

    public override string RoutePrefix => "api/files";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup(string.Empty).RequireAuthorization();
        routes.MapGet("drives", ApiHandlers.Handle<FilesHandlers>(h => h.ListDrivesAsync()));
        routes.MapPost("drives", (HttpContext http, [FromBody] CreateDriveRequest request) => ApiHandlers.For<FilesHandlers>(http).CreateDriveAsync(request));
        routes.MapPost("drives/{id}/inactive", (HttpContext http, [FromRoute] string id, [FromBody] InactiveRequest request) => ApiHandlers.For<FilesHandlers>(http).SetInactiveAsync(id, request));
        routes.MapGet("settings", ApiHandlers.Handle<FilesHandlers>(h => h.GetSettingsAsync()));
        routes.MapPut("settings", (HttpContext http, [FromBody] SaveFileSettingsRequest request) => ApiHandlers.For<FilesHandlers>(http).SaveSettingsAsync(request));
        routes.MapGet("personal-drives/{userId}", (HttpContext http, [FromRoute] string userId) => ApiHandlers.For<FilesHandlers>(http).FindPersonalDriveAsync(userId));
        routes.MapPost("personal-drives/{driveId}/access", (HttpContext http, [FromRoute] string driveId, [FromBody] PersonalDriveAccessRequest request) => ApiHandlers.For<FilesHandlers>(http).RequestAccessAsync(driveId, request));
        routes.MapGet("personal-drives/{driveId}/access", (HttpContext http, [FromRoute] string driveId) => ApiHandlers.For<FilesHandlers>(http).CurrentAccessAsync(driveId));
        routes.MapDelete("personal-drives/access/{accessId}", (HttpContext http, [FromRoute] string accessId) => ApiHandlers.For<FilesHandlers>(http).CloseAccessAsync(accessId));
        routes.MapGet("personal-drives/{userId}/disposal", (HttpContext http, [FromRoute] string userId) => ApiHandlers.For<FilesHandlers>(http).DisposalChoicesAsync(userId));
        routes.MapPost("personal-drives/{userId}/disposal", (HttpContext http, [FromRoute] string userId, [FromBody] DisposalRequest request) => ApiHandlers.For<FilesHandlers>(http).DisposeAsync(userId, request));

        routes.MapGet("{id}", (HttpContext http, [FromRoute] string id) => ApiHandlers.For<FilesHandlers>(http).GetAsync(id));
        routes.MapDelete("{id}", (HttpContext http, [FromRoute] string id) => ApiHandlers.For<FilesHandlers>(http).DeleteAsync(id));
        routes.MapGet("{id}/children", (HttpContext http, [FromRoute] string id, [FromQuery] int? skip, [FromQuery] int? take, [FromQuery] ShareIndicator? access) => ApiHandlers.For<FilesHandlers>(http).ListChildrenAsync(id, skip ?? 0, take ?? 100, access));
        routes.MapPost("{id}/folders", (HttpContext http, [FromRoute] string id, [FromBody] NameRequest request) => ApiHandlers.For<FilesHandlers>(http).CreateFolderAsync(id, request));
        routes.MapPost("{id}/files", (HttpContext http, [FromRoute] string id, [FromForm] IFormFile? file, [FromForm] string? name) => ApiHandlers.For<FilesHandlers>(http).UploadAsync(id, file, name))
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .DisableAntiforgery();
        routes.MapPut("{id}/content", (HttpContext http, [FromRoute] string id, [FromForm] IFormFile? file, [FromQuery] bool? publish) => ApiHandlers.For<FilesHandlers>(http).ReplaceAsync(id, file, publish ?? false))
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .DisableAntiforgery();
        routes.MapPost("{id}/rename", (HttpContext http, [FromRoute] string id, [FromBody] NameRequest request) => ApiHandlers.For<FilesHandlers>(http).RenameAsync(id, request));
        routes.MapPost("{id}/move", (HttpContext http, [FromRoute] string id, [FromBody] PlaceRequest request) => ApiHandlers.For<FilesHandlers>(http).MoveAsync(id, request));
        routes.MapPost("{id}/link", (HttpContext http, [FromRoute] string id, [FromBody] PlaceRequest request) => ApiHandlers.For<FilesHandlers>(http).LinkAsync(id, request));
        routes.MapPost("{id}/shortcut", (HttpContext http, [FromRoute] string id, [FromBody] PlaceRequest request) => ApiHandlers.For<FilesHandlers>(http).ShortcutAsync(id, request));
        routes.MapPut("{id}/metadata", (HttpContext http, [FromRoute] string id, [FromBody] MetadataDto request) => ApiHandlers.For<FilesHandlers>(http).UpdateMetadataAsync(id, request));
        routes.MapGet("{id}/versions", (HttpContext http, [FromRoute] string id) => ApiHandlers.For<FilesHandlers>(http).ListVersionsAsync(id));
        routes.MapPost("{id}/versions/{version:int}/publish", (HttpContext http, [FromRoute] string id, [FromRoute] int version) => ApiHandlers.For<FilesHandlers>(http).PublishAsync(id, version));
        routes.MapGet("{id}/download", (HttpContext http, [FromRoute] string id, [FromQuery] int? version) => ApiHandlers.For<FilesHandlers>(http).DownloadAsync(id, version));
        routes.MapGet("{id}/url", (HttpContext http, [FromRoute] string id, [FromQuery] int? version) => ApiHandlers.For<FilesHandlers>(http).UrlAsync(id, version));
    }
}

/// <summary>The handlers behind <see cref="FilesApi"/>.</summary>
public sealed class FilesHandlers(
    IMediaObjectService files,
    IMediaObjectListing listing,
    IMediaDriveAdministration drives,
    IPersonalDriveAccessService personalAccess,
    IFileDriveSettingsAccessor settings,
    IAccessDecision decision,
    ICallerContextAccessor callers,
    MediaUrlSigners signers) : ApiHandlers
{
    private const int MaxPageSize = 500;
    private static readonly TimeSpan PrivateUrlLifetime = TimeSpan.FromMinutes(5);

    public Task<IResult> ListDrivesAsync() => RunAsync(async () =>
        Ok(await ToDtosAsync(await files.ListDrivesAsync(HttpContext.RequestAborted))));

    public Task<IResult> CreateDriveAsync([FromBody] CreateDriveRequest request) => RunAsync(async () =>
    {
        var owner = request.Type switch
        {
            DriveType.Shared => MediaOwner.Tenant,
            DriveType.Organization => MediaOwner.Organization(request.OwnerId ?? string.Empty),
            _ => MediaOwner.User(request.OwnerId ?? callers.Current?.UserId ?? string.Empty),
        };
        return Ok(await ToDtoAsync(await files.CreateDriveAsync(request.Name, request.Type, owner, HttpContext.RequestAborted)));
    });

    public Task<IResult> GetAsync(string id) => RunAsync(async () =>
        await files.GetAsync(id, HttpContext.RequestAborted) is { } item ? Ok(await ToDtoAsync(item)) : NotFound());

    /// <summary>The children the caller may view, paged after filtering.</summary>
    public Task<IResult> ListChildrenAsync(string id, [FromQuery] int skip = 0, [FromQuery] int take = 100, [FromQuery] ShareIndicator? access = null) => RunAsync(async () =>
    {
        // Filtered and paged in the query while sharing is on (requirement 77), by the access
        // indicator too when asked (docs/sharing.md requirement 98).
        take = Math.Clamp(take, 1, MaxPageSize);
        var page = await listing.ListChildrenPageAsync(id, skip, take, access, HttpContext.RequestAborted);
        return Ok(new FileListDto(await ToDtosAsync(page.Items), page.Total, skip, take));
    });

    public Task<IResult> CreateFolderAsync(string id, [FromBody] NameRequest request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.CreateFolderAsync(id, request.Name, HttpContext.RequestAborted))));

    public Task<IResult> UploadAsync(string id, IFormFile? file, string? name = null) => RunAsync(async () =>
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        await using var stream = file.OpenReadStream();
        var item = await files.UploadAsync(new MediaUpload(id, name ?? file.FileName, stream, file.ContentType), HttpContext.RequestAborted);
        return Ok(await ToDtoAsync(item));
    });

    public Task<IResult> ReplaceAsync(string id, IFormFile? file, bool publish = false) => RunAsync(async () =>
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        // publish: the new version becomes the published one as soon as its verdict is clean
        // (an add-on saving back from the viewer pane, docs/media.md § 8).
        await using var stream = file.OpenReadStream();
        var item = publish && files is IMediaObjectWrites writes
            ? await writes.ReplaceAsync(id, stream, file.ContentType, publishWhenClean: true, HttpContext.RequestAborted)
            : await files.ReplaceAsync(id, stream, file.ContentType, HttpContext.RequestAborted);
        return Ok(await ToDtoAsync(item));
    });

    public Task<IResult> RenameAsync(string id, [FromBody] NameRequest request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.RenameAsync(id, request.Name, HttpContext.RequestAborted))));

    public Task<IResult> MoveAsync(string id, [FromBody] PlaceRequest request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.MoveAsync(id, request.ParentId, HttpContext.RequestAborted))));

    public Task<IResult> LinkAsync(string id, [FromBody] PlaceRequest request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.LinkAsync(id, request.ParentId, request.Name, HttpContext.RequestAborted))));

    public Task<IResult> ShortcutAsync(string id, [FromBody] PlaceRequest request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.ShortcutAsync(id, request.ParentId, request.Name, HttpContext.RequestAborted))));

    public Task<IResult> DeleteAsync(string id) => RunAsync(async () =>
    {
        await files.DeleteAsync(id, HttpContext.RequestAborted);
        return NoContent();
    });

    public Task<IResult> UpdateMetadataAsync(string id, [FromBody] MetadataDto request) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.UpdateMetadataAsync(id, new MediaMetadata(request.Title, request.Description, request.AlternativeText, request.Tags), HttpContext.RequestAborted))));

    public Task<IResult> ListVersionsAsync(string id) => RunAsync(async () =>
        await files.GetAsync(id, HttpContext.RequestAborted) is { } item ? Ok((await ToDtoAsync(item)).Versions) : NotFound());

    public Task<IResult> PublishAsync(string id, int version) => RunAsync(async () =>
        Ok(await ToDtoAsync(await files.PublishVersionAsync(id, version, HttpContext.RequestAborted))));

    public Task<IResult> DownloadAsync(string id, [FromQuery] int? version = null) => RunAsync(async () =>
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
    public Task<IResult> UrlAsync(string id, [FromQuery] int? version = null) => RunAsync(async () =>
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

        // Signed for the private CDN zone when one is configured, else for this origin.
        var expires = DateTimeOffset.UtcNow.Add(PrivateUrlLifetime);
        var signer = signers.Active;
        var signed = signer.Sign(signer.BaseUrl is { } zone ? new Uri(zone + path, UriKind.Absolute) : new Uri(path, UriKind.Relative), expires);
        return Ok(new FileUrlDto(signed.IsAbsoluteUri ? signed.AbsoluteUri : signed.OriginalString, expires));
    });

    // ---- Tenant administration (docs/media.md § 3, § 7) -----------------------------------

    public Task<IResult> GetSettingsAsync() => RunAsync(async () =>
    {
        RequireTenantAdministrator();
        return Ok(new FileSettingsDto(await settings.GetAsync(), await settings.GetPersonalDriveAccessAsync()));
    });

    /// <summary>Saves the settings; a drive type turned off makes its drives inactive, turned
    /// on makes them active again, keeping their sharing or resetting it (requirements 21-22).</summary>
    public Task<IResult> SaveSettingsAsync([FromBody] SaveFileSettingsRequest request) => RunAsync(async () =>
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

                    // Turned on: every organization or staff user without a drive gets one.
                    if (after.Allows(type))
                    {
                        await HttpContext.RequestServices.GetRequiredService<IMediaDriveProvisioning>().ProvisionAllAsync(type, HttpContext.RequestAborted);
                    }
                }
            }
        }

        return NoContent();
    });

    public Task<IResult> SetInactiveAsync(string id, [FromBody] InactiveRequest request) => RunAsync(async () =>
    {
        await drives.SetDriveInactiveAsync(id, request.Inactive, request.ResetSharing, HttpContext.RequestAborted);
        return NoContent();
    });

    /// <summary>The separate "open a user's drive" step (requirement 24): finds the drive, never
    /// what is in it.</summary>
    public Task<IResult> FindPersonalDriveAsync(string userId) => RunAsync(async () =>
        await drives.FindPersonalDriveAsync(userId, HttpContext.RequestAborted) is { } drive
            ? Ok(new PersonalDriveDto(drive.Placement.Id, drive.Placement.Name, drive.Content.Owner.Id, drive.Content.IsInactive, await personalAccess.CurrentAsync(drive.Placement.Id, HttpContext.RequestAborted), (await settings.GetPersonalDriveAccessAsync()).Durations))
            : NotFound());

    public Task<IResult> RequestAccessAsync(string driveId, [FromBody] PersonalDriveAccessRequest request) => RunAsync(async () =>
        Ok(await personalAccess.RequestAsync(driveId, request.UntilClosed ? null : request.Duration, request.OverrideDelay, request.Reason, HttpContext.RequestAborted)));

    public Task<IResult> CurrentAccessAsync(string driveId) => RunAsync(async () =>
        await personalAccess.CurrentAsync(driveId, HttpContext.RequestAborted) is { } access ? Ok(access) : NotFound());

    public Task<IResult> CloseAccessAsync(string accessId) => RunAsync(async () =>
    {
        await personalAccess.CloseAsync(accessId, HttpContext.RequestAborted);
        return NoContent();
    });

    /// <summary>What may happen to a deleted user's personal drive (requirement 20).</summary>
    public Task<IResult> DisposalChoicesAsync(string userId) => RunAsync(() =>
    {
        RequireTenantAdministrator();
        return Task.FromResult<IResult>(Ok(drives.DisposalChoices));
    });

    public Task<IResult> DisposeAsync(string userId, [FromBody] DisposalRequest request) => RunAsync(async () =>
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

    private async Task<IResult> RunAsync(Func<Task<IResult>> action)
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
