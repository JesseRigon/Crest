#nullable enable
using Crest.Security.Services;
using Crest.Media.Services;
using Crest.Media.Sharing;
using Crest.Security;
using Crest.Security.Permissions;
using Crest.Sharing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Media.Library;

/// <summary>
/// Moves who could see the <c>/media</c> tree onto the library drive's placements, while the
/// Sharing feature is on (ruling F1; Secure Media is superseded by sharing on the library):
/// <list type="bullet">
/// <item>sharing entries and rules on media paths (the "media" kind) are copied to the placement
/// each path became (the "file" kind);</item>
/// <item>without Secure Media the media folder was public, so the drive is shared with Anyone as
/// Viewer;</item>
/// <item>with Secure Media, each role's folder rights become Viewer grants: <c>ViewMediaContent</c>
/// on the drive, <c>ViewMediaContent_{folder}</c> on that top folder, <c>ViewRootMediaContent</c>
/// on each file at the root, <c>ViewOthersMediaContent</c> on the users folder. The Anonymous role
/// maps to Anyone and Authenticated to the tenant.</item>
/// </list>
/// Gaps (docs/media.md › Reworking the Media module): a user's own folder (<c>ViewOwnMediaContent</c>)
/// is not granted to its user, and attached-field files do not follow their content item's view
/// right; both stay with the drive's administrators until shared. With sharing off nothing is
/// written: <see cref="LibraryPlacementPermissionMapper"/> decides placements by their path, so
/// Secure Media keeps applying.
/// </summary>
public sealed class MediaLibrarySharingImport(IServiceProvider services, IOptions<MediaOptions> mediaOptions)
{
    public async Task<int> ImportAsync(string driveId, IReadOnlyList<ImportedPlacement> imported, CancellationToken cancellationToken = default)
    {
        if (services.GetService<ISharingService>() is not { } sharing)
        {
            return 0;
        }

        var written = 0;
        foreach (var placement in imported)
        {
            var source = new ShareableRef(MediaShareableKind.KindName, MediaShareableKind.Normalize(placement.Path));
            var target = File(placement.PlacementId);
            foreach (var entry in await sharing.GetEntriesAsync(source, cancellationToken))
            {
                await sharing.SetEntryAsync(target, entry, cancellationToken);
                written++;
            }

            var rules = await sharing.GetRulesAsync(source, cancellationToken);
            if (rules.Count > 0)
            {
                await sharing.SetRulesAsync(target, rules, cancellationToken);
            }
        }

        foreach (var (placementId, entry) in await SecureMediaGrantsAsync(driveId, imported))
        {
            await sharing.SetEntryAsync(File(placementId), entry, cancellationToken);
            written++;
        }

        return written;
    }

    /// <summary>The Viewer grants that reproduce who could see the media folder.</summary>
    public async Task<IReadOnlyList<(string PlacementId, ShareEntry Entry)>> SecureMediaGrantsAsync(string driveId, IReadOnlyList<ImportedPlacement> imported)
    {
        if (!services.IsSecureMediaEnabled())
        {
            return [(driveId, ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer))];
        }

        var roles = services.GetService<IRoleService>();
        if (roles is null)
        {
            return [];
        }

        var usersFolder = mediaOptions.Value.AssetsUsersFolder?.Trim('/') ?? string.Empty;
        var topFolders = imported.Where(placement => placement.IsFolder && placement.Path.Length > 0 && !placement.Path.Contains('/')).ToList();
        var rootFiles = imported.Where(placement => !placement.IsFolder && !placement.Path.Contains('/')).ToList();
        var grants = new List<(string, ShareEntry)>();
        foreach (var role in await roles.GetRolesAsync())
        {
            var permissions = (await roles.GetRoleClaimsAsync(role.RoleName))
                .Where(claim => claim.Type == Permission.ClaimType)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Managing every folder implies viewing every folder.
            if (permissions.Contains(MediaPermissions.ManageMediaFolder.Name))
            {
                permissions.Add(MediaPermissions.ViewMedia.Name);
            }

            var viewer = ShareEntry.Grant(PrincipalFor(role.RoleName), AccessLevel.Viewer);
            if (permissions.Contains(MediaPermissions.ViewMedia.Name))
            {
                grants.Add((driveId, viewer));
                continue;
            }

            foreach (var folder in topFolders)
            {
                var isUsers = string.Equals(folder.Path, usersFolder, StringComparison.Ordinal);
                if (isUsers ? permissions.Contains(MediaPermissions.ViewOthersMedia.Name) : permissions.Contains($"{MediaPermissions.ViewMedia.Name}_{folder.Path}"))
                {
                    grants.Add((folder.PlacementId, viewer));
                }
            }

            if (permissions.Contains(MediaPermissions.ViewRootMedia.Name))
            {
                grants.AddRange(rootFiles.Select(file => (file.PlacementId, viewer)));
            }
        }

        return grants;
    }

    private static SharePrincipal PrincipalFor(string role) => role switch
    {
        _ when string.Equals(role, PlatformConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase) => SharePrincipal.Anyone,
        _ when string.Equals(role, PlatformConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase) => SharePrincipal.Tenant,
        _ => SharePrincipal.Role(role),
    };

    private static ShareableRef File(string placementId) => new(MediaFileShareableKind.KindName, placementId);
}
