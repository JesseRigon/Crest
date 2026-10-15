using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Grove.AdminMenu;
using Grove.Environment.Shell;

namespace Grove.Services;

public sealed class AdminMenuLayoutHub(
    IAuthorizationService authorization,
    ShellSettings shellSettings) : Hub
{
    public const string EventName = "adminMenuLayoutInvalidated";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated != true ||
            !await authorization.AuthorizeAsync(Context.User, AdminMenuPermissions.ManageAdminMenu))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(shellSettings.Name));
        await base.OnConnectedAsync();
    }

    internal static string GroupName(string tenantName) => "grove-admin-menu-layout:" + tenantName;
}

public interface IAdminMenuLayoutInvalidator
{
    Task InvalidateTenantAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminMenuLayoutInvalidator(
    IHubContext<AdminMenuLayoutHub> hub,
    ShellSettings shellSettings) : IAdminMenuLayoutInvalidator
{
    public Task InvalidateTenantAsync(CancellationToken cancellationToken = default) =>
        hub.Clients.Group(AdminMenuLayoutHub.GroupName(shellSettings.Name))
            .SendAsync(AdminMenuLayoutHub.EventName, cancellationToken);
}
