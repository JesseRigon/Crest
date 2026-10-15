using Grove.Access.OpenId.Abstractions.Managers;
using Grove.Security;

namespace Grove.Access.OpenId.Services.Handlers;

public class OpenIdApplicationRoleRemovedEventHandler : IRoleRemovedEventHandler
{
    private readonly IOpenIdApplicationManager _manager;

    public OpenIdApplicationRoleRemovedEventHandler(IOpenIdApplicationManager manager)
        => _manager = manager;

    public async Task RoleRemovedAsync(string roleName)
    {
        await foreach (var application in _manager.ListInRoleAsync(roleName))
        {
            var roles = await _manager.GetRolesAsync(application);
            await _manager.SetRolesAsync(application, roles.Remove(roleName, StringComparer.OrdinalIgnoreCase));
        }
    }
}
