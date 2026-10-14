#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Crest.Api;
using Crest.Admin;
using Crest.Environment.Shell;
using Crest.Security;

namespace Crest.Access;

public sealed class PermissionHub(
    IAuthorizationService authorization,
    ShellSettings shellSettings) : Hub
{
    public const string EventName = "permissionsInvalidated";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated != true ||
            !await authorization.AuthorizeAsync(Context.User, AdminPermissions.AccessAdminPanel))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(shellSettings.Name));
        await base.OnConnectedAsync();
    }

    internal static string GroupName(string tenantName) => "crest-permissions:" + tenantName;
}

/// <summary>
/// Broadcasts an invalidation only. Browsers refetch the normal, authorized
/// manifest instead of receiving permissions through the hub.
/// </summary>


public sealed class PermissionInvalidator(
    IHubContext<PermissionHub> hub,
    ShellSettings shellSettings) : IPermissionInvalidator
{
    public Task InvalidateTenantAsync(CancellationToken cancellationToken = default) =>
        hub.Clients.Group(PermissionHub.GroupName(shellSettings.Name))
            .SendAsync(PermissionHub.EventName, cancellationToken);
}

/// <summary>Receives native Crest role permission updates, including changes made outside Crest.</summary>
public sealed class RolePermissionInvalidationHandler(IPermissionInvalidator invalidator) : IRoleUpdatedEventHandler
{
    public Task RoleUpdatedAsync(string roleName) => invalidator.InvalidateTenantAsync();
}

/// <summary>The permissions hub (<c>api/crest/permissions</c>): admin clients listen for permission changes.</summary>
public sealed class PermissionHubApi : ApiContributor
{
    public override string Name => "PermissionHub";

    public override string FeatureId => "Crest.Access";

    public override void Map(IEndpointRouteBuilder group) => group.MapHub<PermissionHub>("permissions");
}
