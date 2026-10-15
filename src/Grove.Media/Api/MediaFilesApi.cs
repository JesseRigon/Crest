#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.FileStorage;
using Grove.Media;
using Grove.Security.Permissions;

namespace Grove.Media.Api;

public sealed class MediaFilesApi : ApiContributor
{
    public override string Name => "MediaFiles";

    public override string FeatureId => "Grove.Media";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("media");
        routes.MapGet("", (HttpContext __http, [FromQuery] string? path = null) => ApiHandlers.For<MediaFilesHandlers>(__http).ListAsync(path));
        routes.MapPost("folders", (HttpContext __http, [FromBody] MediaFolderRequest request) => ApiHandlers.For<MediaFilesHandlers>(__http).CreateFolderAsync(request));
        routes.MapPost("files", (HttpContext __http, [FromForm] IFormFile file, [FromForm] string? path = null, [FromForm] bool overwrite = false) => ApiHandlers.For<MediaFilesHandlers>(__http).UploadAsync(file, path, overwrite)).WithMetadata(new RequestSizeLimitAttribute(100 * 1024 * 1024)).DisableAntiforgery();
        routes.MapDelete("", (HttpContext __http, [FromQuery] string? path = null) => ApiHandlers.For<MediaFilesHandlers>(__http).DeleteAsync(path));
    }
}

public sealed class MediaFilesHandlers(
    IMediaFileStore mediaFileStore,
    IAuthorizationService authorizationService) : ApiHandlers
{
    public async Task<IResult> ListAsync([FromQuery] string? path = null)
    {
        if (!await CanManageAsync()) return Forbid();

        var normalizedPath = NormalizeDirectory(path);
        if (normalizedPath is null) return BadRequest("The media path is invalid.");

        var entries = new List<MediaEntry>();
        await foreach (var entry in mediaFileStore.GetDirectoryContentAsync(normalizedPath, includeSubDirectories: false))
        {
            entries.Add(new MediaEntry(
                entry.Path,
                entry.Name,
                entry.IsDirectory,
                entry.Length,
                entry.LastModifiedUtc,
                entry.IsDirectory ? null : mediaFileStore.MapPathToPublicUrl(entry.Path)));
        }

        return Ok(new MediaDirectoryResult(normalizedPath, ParentOf(normalizedPath), entries
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray()));
    }

    public async Task<IResult> CreateFolderAsync([FromBody] MediaFolderRequest request)
    {
        if (!await CanManageAsync()) return Forbid();

        var parent = NormalizeDirectory(request.ParentPath);
        var name = NormalizeSegment(request.Name);
        if (parent is null || name is null) return BadRequest("A valid media folder name is required.");

        var path = string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";
        if (!await mediaFileStore.TryCreateDirectoryAsync(path)) return Conflict("The folder could not be created.");
        return await ListAsync(parent);
    }

    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IResult> UploadAsync([FromForm] IFormFile file, [FromForm] string? path = null, [FromForm] bool overwrite = false)
    {
        if (!await CanManageAsync()) return Forbid();
        var directory = NormalizeDirectory(path);
        var name = NormalizeSegment(file.FileName);
        if (directory is null || name is null || file.Length == 0) return BadRequest("A non-empty file with a valid name is required.");

        await mediaFileStore.TryCreateDirectoryAsync(directory);
        await using var stream = file.OpenReadStream();
        await mediaFileStore.CreateFileFromStreamAsync(string.IsNullOrEmpty(directory) ? name : $"{directory}/{name}", stream, overwrite);
        return await ListAsync(directory);
    }

    public async Task<IResult> DeleteAsync([FromQuery] string? path = null)
    {
        if (!await CanManageAsync()) return Forbid();
        var normalizedPath = NormalizeFile(path);
        if (normalizedPath is null) return BadRequest("The media path is invalid.");
        return await mediaFileStore.TryDeleteFileAsync(normalizedPath) ? NoContent() : NotFound();
    }

    private Task<bool> CanManageAsync() => authorizationService.AuthorizeAsync(User, MediaPermissions.ManageMedia);

    private static string? NormalizeDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment => NormalizeSegment(segment) is null) ? null : string.Join('/', segments);
    }

    private static string? NormalizeFile(string? value)
    {
        var directory = NormalizeDirectory(value);
        return string.IsNullOrEmpty(directory) ? null : directory;
    }

    private static string? NormalizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var segment = value.Trim();
        return segment is "." or ".." || segment.Contains('/') || segment.Contains('\\') || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? null : segment;
    }

    private static string? ParentOf(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? (string.IsNullOrEmpty(path) ? null : string.Empty) : path[..separator];
    }
}
