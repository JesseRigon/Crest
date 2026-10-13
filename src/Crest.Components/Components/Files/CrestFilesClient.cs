using System.Net.Http.Json;
using System.Text.Json;

namespace Crest.Components.Files;

// The file-object API (docs/media.md § 3, § 7, § 8) as the drive browser, the viewer pane and the
// picker use it, from either shell. Every call runs as the signed-in caller: the server decides
// what they may see, and what they may not answers 404.

public enum CrestFileKind { File = 0, Folder = 1, Drive = 2 }

public enum CrestDriveType { Shared = 0, Organization = 1, Personal = 2 }

public enum CrestFileVersionState { Quarantined = 0, Clean = 1, Rejected = 2, Failed = 3 }

public sealed record CrestFileMetadata(string? Title, string? Description, string? AlternativeText, IReadOnlyList<string>? Tags);

public sealed record CrestFileVersion(int Number, long Size, string? Type, CrestFileVersionState State, string? Reason, DateTimeOffset CreatedUtc, string? CreatedBy, bool IsPublished);

public sealed record CrestFileItem(
    string Id,
    string ContentId,
    string? ParentId,
    string DriveId,
    string Name,
    CrestFileKind Kind,
    string? ShortcutTargetId,
    CrestDriveType? DriveType,
    bool IsInactive,
    CrestFileMetadata Metadata,
    long? Size,
    string? Type,
    int? PublishedVersion,
    bool CanEdit,
    DateTimeOffset CreatedUtc,
    string? CreatedBy,
    DateTimeOffset ModifiedUtc,
    string? ModifiedBy,
    IReadOnlyList<CrestFileVersion> Versions)
{
    public bool IsContainer => Kind != CrestFileKind.File;

    public string Extension => Path.GetExtension(Name).TrimStart('.').ToLowerInvariant();
}

public sealed record CrestFileList(IReadOnlyList<CrestFileItem> Items, int Total, int Skip, int Take);

public sealed record CrestFileUrl(string Url, DateTimeOffset? Expires);

public sealed record CrestFileDriveSettings(bool OrganizationDrives, bool PersonalDrives, string OrganizationAdminRole);

[Flags]
public enum CrestPersonalDriveNotice { None = 0, Email = 1, Portal = 2 }

public sealed record CrestPersonalDriveAccessSettings(CrestPersonalDriveNotice Notice, TimeSpan Delay, bool AllowDelayOverride, List<TimeSpan?> Durations);

public sealed record CrestFileSettings(CrestFileDriveSettings Drives, CrestPersonalDriveAccessSettings PersonalDriveAccess);

public sealed record CrestPersonalDriveAccess(string Id, string DriveId, string AdministratorUserId, DateTimeOffset RequestedUtc, DateTimeOffset OpensUtc, DateTimeOffset? EndsUtc, bool Overridden, string? Reason, DateTimeOffset? ClosedUtc);

public sealed record CrestPersonalDrive(string DriveId, string Name, string? OwnerUserId, bool IsInactive, CrestPersonalDriveAccess? CurrentAccess, IReadOnlyList<TimeSpan?> Durations);

/// <summary>The outcome of a call: the value, or the server's reason when it refused.</summary>
public sealed record CrestFilesResult<T>(T? Value, string? Error, int Status)
{
    public bool Succeeded => Error is null;
}

public sealed class CrestFilesClient(HttpClient http)
{
    private const string Base = "api/files";

    // Enums travel as numbers, as the server writes and binds them; reading also accepts names.
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<CrestFilesResult<List<CrestFileItem>>> ListDrivesAsync() => GetAsync<List<CrestFileItem>>($"{Base}/drives");

    public Task<CrestFilesResult<CrestFileItem>> GetAsync(string id) => GetAsync<CrestFileItem>($"{Base}/{Uri.EscapeDataString(id)}");

    /// <summary>A page of the folder's children; with <paramref name="access"/> only those with that
    /// access indicator (0 private, 1 shared, 2 public: docs/sharing.md requirement 98).</summary>
    public Task<CrestFilesResult<CrestFileList>> ListChildrenAsync(string id, int skip = 0, int take = 200, int? access = null) =>
        GetAsync<CrestFileList>($"{Base}/{Uri.EscapeDataString(id)}/children?skip={skip}&take={take}{(access is { } indicator ? $"&access={indicator}" : string.Empty)}");

    public Task<CrestFilesResult<CrestFileItem>> CreateDriveAsync(string name, CrestDriveType type, string? ownerId = null) =>
        SendAsync<CrestFileItem>(HttpMethod.Post, $"{Base}/drives", new { name, type, ownerId });

    public Task<CrestFilesResult<CrestFileItem>> CreateFolderAsync(string parentId, string name) =>
        SendAsync<CrestFileItem>(HttpMethod.Post, $"{Base}/{Uri.EscapeDataString(parentId)}/folders", new { name });

    public async Task<CrestFilesResult<CrestFileItem>> UploadAsync(string parentId, string fileName, string? contentType, Stream content)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        if (!string.IsNullOrEmpty(contentType))
        {
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        form.Add(file, "file", fileName);
        using var response = await http.PostAsync($"{Base}/{Uri.EscapeDataString(parentId)}/files", form);
        return await ReadAsync<CrestFileItem>(response);
    }

    /// <param name="publish">Publish the new version as soon as it passes its check.</param>
    public async Task<CrestFilesResult<CrestFileItem>> ReplaceAsync(string id, string fileName, string? contentType, Stream content, bool publish = false)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        if (!string.IsNullOrEmpty(contentType))
        {
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        form.Add(file, "file", fileName);
        using var response = await http.PutAsync($"{Base}/{Uri.EscapeDataString(id)}/content{(publish ? "?publish=true" : string.Empty)}", form);
        return await ReadAsync<CrestFileItem>(response);
    }

    public Task<CrestFilesResult<CrestFileItem>> RenameAsync(string id, string name) =>
        SendAsync<CrestFileItem>(HttpMethod.Post, $"{Base}/{Uri.EscapeDataString(id)}/rename", new { name });

    public Task<CrestFilesResult<CrestFileItem>> MoveAsync(string id, string parentId) =>
        SendAsync<CrestFileItem>(HttpMethod.Post, $"{Base}/{Uri.EscapeDataString(id)}/move", new { parentId });

    public Task<CrestFilesResult<CrestFileItem>> UpdateMetadataAsync(string id, CrestFileMetadata metadata) =>
        SendAsync<CrestFileItem>(HttpMethod.Put, $"{Base}/{Uri.EscapeDataString(id)}/metadata", metadata);

    public Task<CrestFilesResult<CrestFileItem>> PublishVersionAsync(string id, int version) =>
        SendAsync<CrestFileItem>(HttpMethod.Post, $"{Base}/{Uri.EscapeDataString(id)}/versions/{version}/publish", null);

    public Task<CrestFilesResult<bool>> DeleteAsync(string id) => SendAsync<bool>(HttpMethod.Delete, $"{Base}/{Uri.EscapeDataString(id)}", null);

    /// <summary>Where this caller reads the bytes: the plain URL of a public file, a short-lived
    /// signed one otherwise.</summary>
    public Task<CrestFilesResult<CrestFileUrl>> UrlAsync(string id, int? version = null) =>
        GetAsync<CrestFileUrl>($"{Base}/{Uri.EscapeDataString(id)}/url{(version is null ? string.Empty : $"?version={version}")}");

    public string DownloadPath(string id, int? version = null) =>
        $"{Base}/{Uri.EscapeDataString(id)}/download{(version is null ? string.Empty : $"?version={version}")}";

    public Task<CrestFilesResult<CrestFileSettings>> GetSettingsAsync() => GetAsync<CrestFileSettings>($"{Base}/settings");

    public Task<CrestFilesResult<bool>> SaveSettingsAsync(CrestFileSettings settings, bool resetSharingOnReactivation) =>
        SendAsync<bool>(HttpMethod.Put, $"{Base}/settings", new { settings.Drives, settings.PersonalDriveAccess, resetSharingOnReactivation });

    // ---- Administrator access to personal drives (§ 7) ----------------------------------

    public Task<CrestFilesResult<CrestPersonalDrive>> FindPersonalDriveAsync(string userId) =>
        GetAsync<CrestPersonalDrive>($"{Base}/personal-drives/{Uri.EscapeDataString(userId)}");

    public Task<CrestFilesResult<CrestPersonalDriveAccess>> RequestAccessAsync(string driveId, TimeSpan? duration, bool overrideDelay, string? reason) =>
        SendAsync<CrestPersonalDriveAccess>(HttpMethod.Post, $"{Base}/personal-drives/{Uri.EscapeDataString(driveId)}/access", new { duration, untilClosed = duration is null, overrideDelay, reason });

    public Task<CrestFilesResult<CrestPersonalDriveAccess>> CurrentAccessAsync(string driveId) =>
        GetAsync<CrestPersonalDriveAccess>($"{Base}/personal-drives/{Uri.EscapeDataString(driveId)}/access");

    public Task<CrestFilesResult<bool>> CloseAccessAsync(string accessId) =>
        SendAsync<bool>(HttpMethod.Delete, $"{Base}/personal-drives/access/{Uri.EscapeDataString(accessId)}", null);

    private async Task<CrestFilesResult<T>> GetAsync<T>(string uri)
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

    private async Task<CrestFilesResult<T>> SendAsync<T>(HttpMethod method, string uri, object? body)
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

    private static async Task<CrestFilesResult<T>> ReadAsync<T>(HttpResponseMessage response)
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
