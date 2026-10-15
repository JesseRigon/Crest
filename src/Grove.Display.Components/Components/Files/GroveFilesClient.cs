using System.Net.Http.Json;
using System.Text.Json;

namespace Grove.Display.Components.Files;

// The file-object API (docs/media.md § 3, § 7, § 8) as the drive browser, the viewer pane and the
// picker use it, from either shell. Every call runs as the signed-in caller: the server decides
// what they may see, and what they may not answers 404.

public enum GroveFileKind { File = 0, Folder = 1, Drive = 2 }

public enum GroveDriveType { Shared = 0, Organization = 1, Personal = 2 }

public enum GroveFileVersionState { Quarantined = 0, Clean = 1, Rejected = 2, Failed = 3 }

public sealed record GroveFileMetadata(string? Title, string? Description, string? AlternativeText, IReadOnlyList<string>? Tags);

public sealed record GroveFileVersion(int Number, long Size, string? Type, GroveFileVersionState State, string? Reason, DateTimeOffset CreatedUtc, string? CreatedBy, bool IsPublished);

public sealed record GroveFileItem(
    string Id,
    string ContentId,
    string? ParentId,
    string DriveId,
    string Name,
    GroveFileKind Kind,
    string? ShortcutTargetId,
    GroveDriveType? DriveType,
    bool IsInactive,
    GroveFileMetadata Metadata,
    long? Size,
    string? Type,
    int? PublishedVersion,
    bool CanEdit,
    DateTimeOffset CreatedUtc,
    string? CreatedBy,
    DateTimeOffset ModifiedUtc,
    string? ModifiedBy,
    IReadOnlyList<GroveFileVersion> Versions)
{
    public bool IsContainer => Kind != GroveFileKind.File;

    public string Extension => Path.GetExtension(Name).TrimStart('.').ToLowerInvariant();
}

public sealed record GroveFileList(IReadOnlyList<GroveFileItem> Items, int Total, int Skip, int Take);

public sealed record GroveFileUrl(string Url, DateTimeOffset? Expires);

public sealed record GroveFileDriveSettings(bool OrganizationDrives, bool PersonalDrives, string OrganizationAdminRole);

[Flags]
public enum GrovePersonalDriveNotice { None = 0, Email = 1, Portal = 2 }

public sealed record GrovePersonalDriveAccessSettings(GrovePersonalDriveNotice Notice, TimeSpan Delay, bool AllowDelayOverride, List<TimeSpan?> Durations);

public sealed record GroveFileSettings(GroveFileDriveSettings Drives, GrovePersonalDriveAccessSettings PersonalDriveAccess);

public sealed record GrovePersonalDriveAccess(string Id, string DriveId, string AdministratorUserId, DateTimeOffset RequestedUtc, DateTimeOffset OpensUtc, DateTimeOffset? EndsUtc, bool Overridden, string? Reason, DateTimeOffset? ClosedUtc);

public sealed record GrovePersonalDrive(string DriveId, string Name, string? OwnerUserId, bool IsInactive, GrovePersonalDriveAccess? CurrentAccess, IReadOnlyList<TimeSpan?> Durations);

/// <summary>The outcome of a call: the value, or the server's reason when it refused.</summary>
public sealed record GroveFilesResult<T>(T? Value, string? Error, int Status)
{
    public bool Succeeded => Error is null;
}

public sealed class GroveFilesClient(HttpClient http)
{
    private const string Base = "api/files";

    // Enums travel as numbers, as the server writes and binds them; reading also accepts names.
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<GroveFilesResult<List<GroveFileItem>>> ListDrivesAsync() => GetAsync<List<GroveFileItem>>($"{Base}/drives");

    public Task<GroveFilesResult<GroveFileItem>> GetAsync(string id) => GetAsync<GroveFileItem>($"{Base}/{Uri.EscapeDataString(id)}");

    /// <summary>A page of the folder's children; with <paramref name="access"/> only those with that
    /// access indicator (0 private, 1 shared, 2 public: docs/sharing.md requirement 98).</summary>
    public Task<GroveFilesResult<GroveFileList>> ListChildrenAsync(string id, int skip = 0, int take = 200, int? access = null) =>
        GetAsync<GroveFileList>($"{Base}/{Uri.EscapeDataString(id)}/children?skip={skip}&take={take}{(access is { } indicator ? $"&access={indicator}" : string.Empty)}");

    public Task<GroveFilesResult<GroveFileItem>> CreateDriveAsync(string name, GroveDriveType type, string? ownerId = null) =>
        SendAsync<GroveFileItem>(HttpMethod.Post, $"{Base}/drives", new { name, type, ownerId });

    public Task<GroveFilesResult<GroveFileItem>> CreateFolderAsync(string parentId, string name) =>
        SendAsync<GroveFileItem>(HttpMethod.Post, $"{Base}/{Uri.EscapeDataString(parentId)}/folders", new { name });

    public async Task<GroveFilesResult<GroveFileItem>> UploadAsync(string parentId, string fileName, string? contentType, Stream content)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        if (!string.IsNullOrEmpty(contentType))
        {
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        form.Add(file, "file", fileName);
        using var response = await http.PostAsync($"{Base}/{Uri.EscapeDataString(parentId)}/files", form);
        return await ReadAsync<GroveFileItem>(response);
    }

    /// <param name="publish">Publish the new version as soon as it passes its check.</param>
    public async Task<GroveFilesResult<GroveFileItem>> ReplaceAsync(string id, string fileName, string? contentType, Stream content, bool publish = false)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        if (!string.IsNullOrEmpty(contentType))
        {
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        form.Add(file, "file", fileName);
        using var response = await http.PutAsync($"{Base}/{Uri.EscapeDataString(id)}/content{(publish ? "?publish=true" : string.Empty)}", form);
        return await ReadAsync<GroveFileItem>(response);
    }

    public Task<GroveFilesResult<GroveFileItem>> RenameAsync(string id, string name) =>
        SendAsync<GroveFileItem>(HttpMethod.Post, $"{Base}/{Uri.EscapeDataString(id)}/rename", new { name });

    public Task<GroveFilesResult<GroveFileItem>> MoveAsync(string id, string parentId) =>
        SendAsync<GroveFileItem>(HttpMethod.Post, $"{Base}/{Uri.EscapeDataString(id)}/move", new { parentId });

    public Task<GroveFilesResult<GroveFileItem>> UpdateMetadataAsync(string id, GroveFileMetadata metadata) =>
        SendAsync<GroveFileItem>(HttpMethod.Put, $"{Base}/{Uri.EscapeDataString(id)}/metadata", metadata);

    public Task<GroveFilesResult<GroveFileItem>> PublishVersionAsync(string id, int version) =>
        SendAsync<GroveFileItem>(HttpMethod.Post, $"{Base}/{Uri.EscapeDataString(id)}/versions/{version}/publish", null);

    public Task<GroveFilesResult<bool>> DeleteAsync(string id) => SendAsync<bool>(HttpMethod.Delete, $"{Base}/{Uri.EscapeDataString(id)}", null);

    /// <summary>Where this caller reads the bytes: the plain URL of a public file, a short-lived
    /// signed one otherwise.</summary>
    public Task<GroveFilesResult<GroveFileUrl>> UrlAsync(string id, int? version = null) =>
        GetAsync<GroveFileUrl>($"{Base}/{Uri.EscapeDataString(id)}/url{(version is null ? string.Empty : $"?version={version}")}");

    public string DownloadPath(string id, int? version = null) =>
        $"{Base}/{Uri.EscapeDataString(id)}/download{(version is null ? string.Empty : $"?version={version}")}";

    public Task<GroveFilesResult<GroveFileSettings>> GetSettingsAsync() => GetAsync<GroveFileSettings>($"{Base}/settings");

    public Task<GroveFilesResult<bool>> SaveSettingsAsync(GroveFileSettings settings, bool resetSharingOnReactivation) =>
        SendAsync<bool>(HttpMethod.Put, $"{Base}/settings", new { settings.Drives, settings.PersonalDriveAccess, resetSharingOnReactivation });

    // ---- Administrator access to personal drives (§ 7) ----------------------------------

    public Task<GroveFilesResult<GrovePersonalDrive>> FindPersonalDriveAsync(string userId) =>
        GetAsync<GrovePersonalDrive>($"{Base}/personal-drives/{Uri.EscapeDataString(userId)}");

    public Task<GroveFilesResult<GrovePersonalDriveAccess>> RequestAccessAsync(string driveId, TimeSpan? duration, bool overrideDelay, string? reason) =>
        SendAsync<GrovePersonalDriveAccess>(HttpMethod.Post, $"{Base}/personal-drives/{Uri.EscapeDataString(driveId)}/access", new { duration, untilClosed = duration is null, overrideDelay, reason });

    public Task<GroveFilesResult<GrovePersonalDriveAccess>> CurrentAccessAsync(string driveId) =>
        GetAsync<GrovePersonalDriveAccess>($"{Base}/personal-drives/{Uri.EscapeDataString(driveId)}/access");

    public Task<GroveFilesResult<bool>> CloseAccessAsync(string accessId) =>
        SendAsync<bool>(HttpMethod.Delete, $"{Base}/personal-drives/access/{Uri.EscapeDataString(accessId)}", null);

    private async Task<GroveFilesResult<T>> GetAsync<T>(string uri)
    {
        try
        {
            using var response = await http.GetAsync(uri);
            return await ReadAsync<T>(response);
        }
        catch (HttpRequestException exception)
        {
            return new(default, exception.Message, 0);
        }
    }

    private async Task<GroveFilesResult<T>> SendAsync<T>(HttpMethod method, string uri, object? body)
    {
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }

            using var response = await http.SendAsync(request);
            return await ReadAsync<T>(response);
        }
        catch (HttpRequestException exception)
        {
            return new(default, exception.Message, 0);
        }
    }

    private static async Task<GroveFilesResult<T>> ReadAsync<T>(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            return new(default, string.IsNullOrWhiteSpace(text) ? response.ReasonPhrase ?? $"HTTP {status}" : text.Trim('"'), status);
        }

        if (typeof(T) == typeof(bool))
        {
            return new((T)(object)true, null, status);
        }

        return new(await response.Content.ReadFromJsonAsync<T>(Json), null, status);
    }
}
