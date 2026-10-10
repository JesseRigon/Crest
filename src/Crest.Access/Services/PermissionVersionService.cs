using Crest.Data.Documents;
using Crest.Documents;
using Crest.Security;
using Crest.Users.Handlers;

namespace Crest.Access.Services;

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

/// <summary>Bumps the version on every event that can change what a caller holds.</summary>
public sealed class PermissionVersionBumper(IPermissionVersion version)
    : IRoleCreatedEventHandler, IRoleUpdatedEventHandler, IRoleRemovedEventHandler, IUserEventHandler
{
    public Task RoleCreatedAsync(string roleName) => version.BumpAsync();

    public Task RoleUpdatedAsync(string roleName) => version.BumpAsync();

    public Task RoleRemovedAsync(string roleName) => version.BumpAsync();

    public Task CreatingAsync(UserCreateContext context) => Task.CompletedTask;

    public Task CreatedAsync(UserCreateContext context) => version.BumpAsync();

    public Task DeletingAsync(UserDeleteContext context) => Task.CompletedTask;

    public Task DeletedAsync(UserDeleteContext context) => version.BumpAsync();

    public Task UpdatingAsync(UserUpdateContext context) => Task.CompletedTask;

    public Task UpdatedAsync(UserUpdateContext context) => version.BumpAsync();

    public Task DisabledAsync(UserContext context) => version.BumpAsync();

    public Task EnabledAsync(UserContext context) => version.BumpAsync();

    public Task ConfirmedAsync(UserConfirmContext context) => Task.CompletedTask;
}
