#nullable enable
using Crest.Access;
using Crest.Media.Files;
using Crest.Media.Services;

namespace Crest.Media.Library;

/// <summary>
/// With sharing off, a placement in the media library is decided as its path was (ruling F1): the
/// library keeps the rights the <c>/media</c> tree had. Viewing re-asks <c>ViewMedia</c> for the
/// path, so Secure Media's per-folder rights still apply; without Secure Media the library is
/// public, as <c>/media</c> was. Editing and managing re-ask <c>ManageMediaFolder</c> for the
/// path (own, others' and attached-field folders included). With sharing on, sharing is the
/// authority over placements and this mapper is never asked. Placements outside the library are
/// not its business.
/// </summary>
public sealed class LibraryPlacementPermissionMapper(IMediaLibrary library, IServiceProvider services) : IResourcePermissionMapper
{
    public async ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var placement = resource switch
        {
            MediaPlacement found => found,
            MediaItem item => item.Placement,
            _ => null,
        };

        if (placement is null || !IsMediaPermission(permission))
        {
            return null;
        }

        var driveId = await library.GetDriveIdAsync(cancellationToken);
        if (driveId is null || placement.DriveId != driveId)
        {
            return null;
        }

        var path = await library.GetPathAsync(placement.LinkTargetPlacementId ?? placement.Id, cancellationToken) ?? string.Empty;
        if (permission == MediaPermissions.ViewMedia.Name)
        {
            return services.IsSecureMediaEnabled()
                ? [new PermissionCandidate(MediaPermissions.ViewMedia.Name, path)]
                : [PermissionCandidate.Granted];
        }

        return [new PermissionCandidate(MediaPermissions.ManageMediaFolder.Name, path)];
    }

    private static bool IsMediaPermission(string permission) =>
        permission == MediaPermissions.ViewMedia.Name ||
        permission == MediaPermissions.EditMedia.Name ||
        permission == MediaPermissions.ManageMedia.Name;
}
