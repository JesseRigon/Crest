#nullable enable
using Grove.Environment.Shell.Scope;
using Grove.Media.Library;
using Grove.Access.Users;
using Grove.Access.Users.Handlers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grove.Media.Files;

/// <summary>
/// Organization and personal drives created automatically (docs/media.md § 3, requirements 12
/// and 18): with each organization and staff user while the tenant uses the drive type, and for
/// everyone the <see cref="IMediaDriveOwnerSource"/>s know when it is turned on. As the system
/// actor, through the file-object service, so the drive gets its defaults (an organization
/// drive's Admin grant to the organization's admins, requirement 17). An owner whose drive
/// exists, active or not, keeps it: turning a type back on reactivates drives, never doubles them.
/// </summary>
public sealed class MediaDriveProvisioner(
    IServiceProvider services,
    IMediaObjectStore store,
    IFileDriveSettingsAccessor settings,
    IEnumerable<IMediaDriveOwnerSource> sources,
    ILogger<MediaDriveProvisioner> logger) : IMediaDriveProvisioning
{
    public async Task<MediaItem?> EnsureOrganizationDriveAsync(string organizationId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        if (!(await settings.GetAsync()).OrganizationDrives)
        {
            return null;
        }

        if (await store.GetOrganizationDriveAsync(organizationId) is { } existing)
        {
            return await ItemAsync(existing);
        }

        return await CreateAsync(DriveName(name, organizationId), DriveType.Organization, MediaOwner.Organization(organizationId), cancellationToken);
    }

    public async Task<MediaItem?> EnsurePersonalDriveAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        if (!(await settings.GetAsync()).PersonalDrives)
        {
            return null;
        }

        if (await store.GetPersonalDriveAsync(userId) is { } existing)
        {
            return await ItemAsync(existing);
        }

        // Members never have a personal drive (requirement 18). A user no source knows is staff:
        // every user created outside the member portal is.
        foreach (var source in sources)
        {
            if (await source.IsStaffAsync(userId, cancellationToken) == false)
            {
                return null;
            }
        }

        return await CreateAsync(DriveName(name, userId), DriveType.Personal, MediaOwner.User(userId), cancellationToken);
    }

    public async Task<int> ProvisionAllAsync(DriveType type, CancellationToken cancellationToken = default)
    {
        if (type == DriveType.Shared)
        {
            return 0;
        }

        var created = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            await foreach (var owner in source.GetOwnersAsync(type, cancellationToken))
            {
                if (!seen.Add(owner.Id))
                {
                    continue;
                }

                var exists = type == DriveType.Personal
                    ? await store.GetPersonalDriveAsync(owner.Id) is not null
                    : await store.GetOrganizationDriveAsync(owner.Id) is not null;
                if (exists)
                {
                    continue;
                }

                var drive = type == DriveType.Personal
                    ? await EnsurePersonalDriveAsync(owner.Id, owner.Name, cancellationToken)
                    : await EnsureOrganizationDriveAsync(owner.Id, owner.Name, cancellationToken);
                if (drive is not null)
                {
                    created++;
                }
            }
        }

        return created;
    }

    private async Task<MediaItem?> CreateAsync(string name, DriveType type, MediaOwner owner, CancellationToken cancellationToken)
    {
        try
        {
            return await MediaCallers.AsSystemAsync(services, () =>
                services.GetRequiredService<IMediaObjectService>().CreateDriveAsync(name, type, owner, cancellationToken));
        }
        catch (MediaObjectConflictException exception)
        {
            // Created meanwhile, or the type was turned off: nothing to do.
            logger.LogDebug(exception, "No {Type} drive created for {Owner}.", type, owner.Id);
            return null;
        }
    }

    private async Task<MediaItem?> ItemAsync(MediaContentDocument content)
    {
        var placement = (await store.GetPlacementsOfContentAsync(content.Content.Id)).FirstOrDefault();
        return placement is null ? null : new MediaItem(placement.Placement, content.Content);
    }

    /// <summary>A valid drive name from a display name, falling back to the owner id.</summary>
    private static string DriveName(string? name, string fallback)
    {
        try
        {
            return MediaNames.Validate(string.IsNullOrWhiteSpace(name) ? fallback : name.Trim());
        }
        catch (ArgumentException)
        {
            return MediaNames.Validate(fallback);
        }
    }
}

/// <summary>
/// A personal drive with each new staff user (requirement 12). After the request's unit of work:
/// a member's class is stamped while the user is created, and the drive's own writes must not
/// enter the user's uncommitted session.
/// </summary>
// UserManager is resolved when a user is created, not injected: it needs the user store, and the
// user store takes every IUserEventHandler, this one included - a constructor dependency is a
// cycle the container cannot see through the store's factory registrations (it recursed until
// the request timed out on a fresh install).
public sealed class PersonalDriveUserEventHandler(IServiceProvider services) : UserEventHandlerBase
{
    public override async Task CreatedAsync(UserCreateContext context)
    {
        var users = services.GetRequiredService<UserManager<IUser>>();
        var userId = await users.GetUserIdAsync(context.User);
        var userName = context.User.UserName;
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        ShellScope.AddDeferredTask(scope =>
            scope.ServiceProvider.GetRequiredService<IMediaDriveProvisioning>().EnsurePersonalDriveAsync(userId, userName));
    }
}
