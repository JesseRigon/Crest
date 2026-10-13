using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell;
using Crest.Modules;
using Crest.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Roles.Services;

/// <summary>
/// Crest's role registry (docs/sharing.md § 1 and § 5, requirements 40, 44, 48): the stored
/// roles are <see cref="RoleKind.Assigned"/>, and every role an enabled
/// <see cref="IDerivedRoleProvider"/> computes is <see cref="RoleKind.Derived"/>. A derived role
/// is still a real role: <see cref="EnsureDerivedRolesAsync"/> stores it, with no permissions,
/// so it has a stable identity, shows in pickers and permission screens, and takes whatever
/// permissions the tenant gives it there.
/// </summary>
public sealed class RoleRegistry(
    RoleManager<IRole> roleManager,
    IEnumerable<IDerivedRoleProvider> providers) : IRoleRegistry
{
    public Task<IReadOnlyList<RegisteredRole>> ListAsync(CancellationToken cancellationToken = default)
    {
        var roles = new Dictionary<string, RegisteredRole>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roleManager.Roles)
        {
            roles[role.RoleName] = new RegisteredRole(role.RoleName, RoleKind.Assigned, null, role.RoleDescription);
        }

        foreach (var provider in providers)
        {
            foreach (var definition in provider.Roles)
            {
                roles[definition.RoleName] = new RegisteredRole(definition.RoleName, RoleKind.Derived, provider.Name, definition.Description);
            }
        }

        return Task.FromResult<IReadOnlyList<RegisteredRole>>([.. roles.Values.OrderBy(role => role.RoleName, StringComparer.OrdinalIgnoreCase)]);
    }

    public RoleKind KindOf(string roleName) => DerivedRoles.IsDerived(providers, roleName) ? RoleKind.Derived : RoleKind.Assigned;

    public Task<IReadOnlyList<DerivedRoleGrant>> DerivedRolesAsync(DerivedRoleContext context, CancellationToken cancellationToken = default) =>
        DerivedRoles.GrantsAsync(providers, context, cancellationToken);

    public async Task EnsureDerivedRolesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var definition in providers.SelectMany(provider => provider.Roles))
        {
            if (await roleManager.FindByNameAsync(definition.RoleName) is not null)
            {
                continue;
            }

            await roleManager.CreateAsync(new Role
            {
                RoleName = definition.RoleName,
                RoleDescription = definition.Description ?? $"Derived role: held through a relationship, never assigned.",
            });
        }
    }
}

/// <summary>A feature enabled may bring a derived-role provider: its roles are stored then.</summary>
public sealed class DerivedRolesFeatureEvents(IRoleRegistry registry) : FeatureEventHandler
{
    public override Task EnabledAsync(IFeatureInfo feature) => registry.EnsureDerivedRolesAsync();
}

[Feature("Crest.Roles.Core")]
public sealed class RoleRegistryStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRoleRegistry, RoleRegistry>();
        services.AddScoped<IFeatureEventHandler, DerivedRolesFeatureEvents>();
    }
}
