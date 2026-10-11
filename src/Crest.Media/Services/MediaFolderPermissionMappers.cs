using Crest.Access;
using Crest.ContentManagement;
using Crest.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Media.Services;

/// <summary>What the two media folder mappers share: path normalisation and folder tests.</summary>
public abstract class MediaFolderPermissionMapperBase(IMediaFileStore fileStore, AttachedMediaFieldFileService attachedMediaFieldFileService, IOptions<MediaOptions> options, IUserAssetFolderNameProvider userAssetFolderNameProvider)
{
    protected const char PathSeparator = '/';

    protected IMediaFileStore FileStore { get; } = fileStore;
    protected MediaOptions MediaOptions { get; } = options.Value;
    protected string MediaFieldsFolder { get; } = EnsureTrailingSlash(fileStore, attachedMediaFieldFileService.MediaFieldsFolder);
    protected string UsersFolder { get; } = EnsureTrailingSlash(fileStore, options.Value.AssetsUsersFolder);

    protected string? UserOwnFolder(CallerContext caller)
    {
        var name = userAssetFolderNameProvider.GetUserAssetFolderName(caller.UserId);
        return string.IsNullOrEmpty(name) ? null : EnsureTrailingSlash(FileStore, FileStore.Combine(UsersFolder, name));
    }

    protected bool IsFolder(string authorizedFolder, string? childPath)
        => EnsureTrailingSlash(FileStore, childPath ?? string.Empty).Equals(authorizedFolder, StringComparison.Ordinal);

    protected bool IsDescendantOf(string authorizedFolder, string? childPath)
        => FileStore.NormalizePath(childPath ?? string.Empty).StartsWith(authorizedFolder, StringComparison.Ordinal);

    protected static string EnsureTrailingSlash(IMediaFileStore fileStore, string path)
        => fileStore.NormalizePath(path) + PathSeparator;
}

/// <summary>
/// <c>ManageMediaFolder</c> asked against a folder path: the attached-fields folder takes
/// <c>ManageAttachedMediaFieldsFolder</c>, the caller's own user folder <c>ManageOwnMedia</c>,
/// another user's <c>ManageOthersMedia</c>, anything else <c>ManageMedia</c>. With secure
/// media enabled the caller must also be allowed to view the folder.
/// </summary>
public sealed class ManageMediaFolderPermissionMapper(
    IMediaFileStore fileStore,
    AttachedMediaFieldFileService attachedMediaFieldFileService,
    IOptions<MediaOptions> options,
    IUserAssetFolderNameProvider userAssetFolderNameProvider,
    IServiceProvider serviceProvider)
    : MediaFolderPermissionMapperBase(fileStore, attachedMediaFieldFileService, options, userAssetFolderNameProvider), IResourcePermissionMapper
{
    public async ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (permission != MediaPermissions.ManageMediaFolder.Name || resource is not string resourcePath)
        {
            return null;
        }

        var path = await FileStore.ResolveAuthorizedPathAsync(resourcePath);
        var userOwnFolder = UserOwnFolder(caller);
        var variant = MediaPermissions.ManageMedia;

        if (IsFolder(MediaFieldsFolder, path) || IsDescendantOf(MediaFieldsFolder, path))
        {
            variant = MediaPermissions.ManageAttachedMediaFieldsFolder;
        }

        if (IsFolder(UsersFolder, path) || (userOwnFolder is not null && (IsFolder(userOwnFolder, path) || IsDescendantOf(userOwnFolder, path))))
        {
            variant = MediaPermissions.ManageOwnMedia;
        }

        if (IsDescendantOf(UsersFolder, path) && (userOwnFolder is null || (!IsFolder(userOwnFolder, path) && !IsDescendantOf(userOwnFolder, path))))
        {
            variant = MediaPermissions.ManageOthersMedia;
        }

        // Managing a secured folder also takes viewing it: a conjunction the candidate list
        // cannot express, so the view decision is asked here first.
        if (serviceProvider.IsSecureMediaEnabled())
        {
            var decision = serviceProvider.GetRequiredService<IAccessDecision>();
            if (!(await decision.DecideAsync(caller, MediaPermissions.ViewMedia.Name, path, cancellationToken)).IsAllowed)
            {
                return [];
            }
        }

        return [variant.Name, permission];
    }
}

/// <summary>
/// <c>ViewMedia</c> asked against a path, with secure media enabled: the root takes
/// <c>ViewRootMedia</c> (the provider's instance, implied by every first-level folder
/// permission), a user folder <c>ViewOwnMedia</c> or <c>ViewOthersMedia</c>, an attached
/// field's folder the content item's <c>ViewContent</c> (re-asked for the item), any other
/// folder its own dynamic permission; a path outside the secured folders is public.
/// </summary>
public sealed class ViewMediaFolderPermissionMapper(
    IMediaFileStore fileStore,
    AttachedMediaFieldFileService attachedMediaFieldFileService,
    IOptions<MediaOptions> options,
    IUserAssetFolderNameProvider userAssetFolderNameProvider,
    IContentManager contentManager)
    : MediaFolderPermissionMapperBase(fileStore, attachedMediaFieldFileService, options, userAssetFolderNameProvider), IResourcePermissionMapper
{
    public async ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (permission != MediaPermissions.ViewMedia.Name || resource is not string resourcePath)
        {
            return null;
        }

        var path = await FileStore.ResolveAuthorizedPathAsync(resourcePath);

        // Permissions are set for root folders; sub folders are checked through them.
        var i = path.IndexOf(PathSeparator);
        var folderPath = i >= 0 ? path[..i] : path;
        var directory = await FileStore.GetDirectoryInfoAsync(folderPath);
        if (directory is null && path.IndexOf(PathSeparator, folderPath.Length) < 0)
        {
            // A new directory, or a new or existing file in the root folder: a file is told by
            // its extension; otherwise the request is about the root itself.
            if (await FileStore.GetFileInfoAsync(folderPath) is not null ||
                MediaOptions.IsFileExtensionAllowed(Path.GetExtension(path), hasAdditionalPermission: true))
            {
                path = string.Empty;
            }
        }

        if (IsFolder("/", path))
        {
            return [MediaPermissions.ViewRootMedia.Name];
        }

        if (IsFolder(MediaFieldsFolder, path) || IsDescendantOf(MediaFieldsFolder, path))
        {
            return await AttachedMediaFieldsFolderAsync(caller, path);
        }

        if (IsFolder(UsersFolder, path) || IsDescendantOf(UsersFolder, path))
        {
            return [UsersFolderPermission(caller, path).Name];
        }

        // A dynamic permission per first-level folder, so access can be given to specific
        // folders only; without a template (secure media off) the file is public.
        var template = SecureMediaPermissions.ConvertToDynamicPermission(MediaPermissions.ViewMedia);
        return template is null ? [PermissionCandidate.Granted] : [SecureMediaPermissions.CreateDynamicPermission(template, folderPath).Name];
    }

    private async ValueTask<IReadOnlyList<PermissionCandidate>> AttachedMediaFieldsFolderAsync(CallerContext caller, string path)
    {
        var parts = path[(MediaFieldsFolder.Length - 1)..].Split(PathSeparator, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            // The media fields folder itself is never served.
            return [];
        }

        if (string.Equals(parts[0], "temp", StringComparison.OrdinalIgnoreCase))
        {
            // Per-user temporary files.
            var userId = parts.Length > 1 ? parts[1] : null;
            var own = UserOwnFolderName(caller) is { } ownName && IsFolder(EnsureTrailingSlash(FileStore, ownName), userId);
            return [own ? MediaPermissions.ViewOwnMedia.Name : MediaPermissions.ViewOthersMedia.Name];
        }

        // The content item's own view right decides its attached media.
        var contentItemId = parts.Length > 1 ? parts[1] : null;
        var contentItem = string.IsNullOrEmpty(contentItemId) ? null : await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        return contentItem is null ? [] : [new PermissionCandidate(Contents.CommonPermissions.ViewContent.Name, contentItem)];
    }

    private Crest.Security.Permissions.Permission UsersFolderPermission(CallerContext caller, string path)
    {
        // The users folder itself is own media (listing it); another user's folder is others'.
        if (path.IndexOf(PathSeparator) < 0)
        {
            return MediaPermissions.ViewOwnMedia;
        }

        var userOwnFolder = UserOwnFolder(caller);
        return userOwnFolder is not null && (IsFolder(userOwnFolder, path) || IsDescendantOf(userOwnFolder, path))
            ? MediaPermissions.ViewOwnMedia
            : MediaPermissions.ViewOthersMedia;
    }

    private string? UserOwnFolderName(CallerContext caller)
    {
        var name = userAssetFolderNameProvider.GetUserAssetFolderName(caller.UserId);
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
