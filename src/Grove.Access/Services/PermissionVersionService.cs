using Grove.Data.Documents;
using Grove.Documents;
using Grove.Security;
using Grove.Users.Handlers;

namespace Grove.Access.Services;

public sealed class PermissionVersionDocument : Document
{
    public long Version { get; set; }
}

/// <summary>
/// The tenant's permission version, in a cached document: read on every caller build (a
/// cache hit), bumped by role, binding and policy writes.
/// </summary>
public sealed class PermissionVersionService(IDocumentManager<PermissionVersionDocument> documents) : IPermissionVersion
{
    public async Task<long> GetAsync(CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateImmutableAsync();
        return document.Version;
    }

    public async Task BumpAsync(CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateMutableAsync();
        document.Version++;
        await documents.UpdateAsync(document);
    }
}

/// <summary>
/// Bumps the version on the rights writes these events stand for (docs/access.md, Invalidation):
/// roles created, changed or removed; a user deleted, disabled or enabled. A user created holds
/// no cached state yet, and an ordinary update (a profile, a lockout counter, a sign-in) changes
/// no right; a user's role membership bumps where it is written (the user store).
/// </summary>
public sealed class PermissionVersionBumper(IPermissionVersion version)
    : IRoleCreatedEventHandler, IRoleUpdatedEventHandler, IRoleRemovedEventHandler, IUserEventHandler
{
    public Task RoleCreatedAsync(string roleName) => version.BumpAsync();

    public Task RoleUpdatedAsync(string roleName) => version.BumpAsync();

    public Task RoleRemovedAsync(string roleName) => version.BumpAsync();

    public Task CreatingAsync(UserCreateContext context) => Task.CompletedTask;

    public Task CreatedAsync(UserCreateContext context) => Task.CompletedTask;

    public Task DeletingAsync(UserDeleteContext context) => Task.CompletedTask;

    public Task DeletedAsync(UserDeleteContext context) => version.BumpAsync();

    public Task UpdatingAsync(UserUpdateContext context) => Task.CompletedTask;

    public Task UpdatedAsync(UserUpdateContext context) => Task.CompletedTask;

    public Task DisabledAsync(UserContext context) => version.BumpAsync();

    public Task EnabledAsync(UserContext context) => version.BumpAsync();

    public Task ConfirmedAsync(UserConfirmContext context) => Task.CompletedTask;
}
