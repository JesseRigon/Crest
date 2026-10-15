#nullable enable
using Grove.Security.Services;
using Grove.Media.Services;
using Grove.Media.Sharing;
using Grove.Security;
using Grove.Security.Permissions;
using Grove.Access.Sharing;
using YesSql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Media.Library;

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
/// Each user's own folder is granted to its user (Viewer with <c>ViewOwnMediaContent</c>, Manager
/// with <c>ManageOwnMediaContent</c>). Attached-field files follow their content item: the file
/// kind gives each content item's attached folder the item as a further parent
/// (<see cref="Sharing.MediaFileShareableKind"/>). With sharing off nothing is
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
        var rolePermissions = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in await roles.GetRolesAsync())
        {
            var permissions = (await roles.GetRoleClaimsAsync(role.RoleName))
                .Where(claim => claim.Type == Permission.ClaimType)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            rolePermissions[role.RoleName] = permissions;

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

        grants.AddRange(await OwnFolderGrantsAsync(usersFolder, imported, rolePermissions));
        return grants;
    }

    /// <summary>
    /// Each user's own folder (<c>{users folder}/{the user's folder name}</c>) granted to that user,
    /// as Secure Media let them have it: Manager when one of their roles manages its own media,
    /// Viewer when one may view its own media. Every signed-in user holds the Authenticated role.
    /// </summary>
    private async Task<IReadOnlyList<(string PlacementId, ShareEntry Entry)>> OwnFolderGrantsAsync(string usersFolder, IReadOnlyList<ImportedPlacement> imported, IReadOnlyDictionary<string, HashSet<string>> rolePermissions)
    {
        if (usersFolder.Length == 0 ||
            services.GetService<IUserAssetFolderNameProvider>() is not { } folderNames ||
            services.GetService<Microsoft.AspNetCore.Identity.UserManager<Grove.Access.Users.IUser>>() is not { } users ||
            services.GetService<YesSql.ISession>() is not { } session)
        {
            return [];
        }

        var userFolders = imported
            .Where(placement => placement.IsFolder && placement.Path.StartsWith(usersFolder + "/", StringComparison.Ordinal) && placement.Path.Count(character => character == '/') == 1)
            .ToDictionary(placement => placement.Path[(usersFolder.Length + 1)..], placement => placement.PlacementId, StringComparer.Ordinal);
        if (userFolders.Count == 0)
        {
            return [];
        }

        var grants = new List<(string, ShareEntry)>();
        foreach (var index in await session.QueryIndex<Grove.Access.Users.Indexes.UserIndex>().ListAsync())
        {
            if (folderNames.GetUserAssetFolderName(index.UserId) is not { } name ||
                !userFolders.TryGetValue(name, out var placementId) ||
                await users.FindByIdAsync(index.UserId) is not { } user)
            {
                continue;
            }

            var held = (await users.GetRolesAsync(user)).Append(PlatformConstants.Roles.Authenticated)
                .SelectMany(role => rolePermissions.TryGetValue(role, out var permissions) ? permissions : [])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var level = held.Contains(MediaPermissions.ManageOwnMedia.Name) || held.Contains(MediaPermissions.ManageOthersMedia.Name) || held.Contains(MediaPermissions.ManageMedia.Name)
                ? AccessLevel.Manager
                : held.Contains(MediaPermissions.ViewOwnMedia.Name) ? AccessLevel.Viewer : AccessLevel.None;
            if (level != AccessLevel.None)
            {
                grants.Add((placementId, ShareEntry.Grant(SharePrincipal.User(index.UserId), level)));
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
