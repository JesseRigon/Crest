#nullable enable

using System.Security.Claims;
using Crest.Access;
using Crest.Api;
using Crest.Security.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Crest.Roles.Api;

/// <summary>The roles list (<c>api/crest/roles</c>).</summary>
public sealed class RolesApi : ApiContributor
{
    public override string Name => "Roles";

    public override string FeatureId => "Crest.Roles";

    public override void Map(IEndpointRouteBuilder group) =>
        group.MapGet("roles", ListAsync);

    private static async Task<IResult> ListAsync(ClaimsPrincipal user, [FromServices] IRoleService roleService, [FromServices] IAuthorizationService authorization)
    {
        if (!await authorization.AuthorizeAsync(user, AccessPermissions.ReadRoles))
        {
            return TypedResults.Forbid();
        }

        var result = new List<RoleSummary>();
        foreach (var role in await roleService.GetRolesAsync())
        {
            result.Add(new RoleSummary(
                role.RoleName,
                role.RoleDescription,
                await roleService.IsAdminRoleAsync(role.RoleName),
                await roleService.IsSystemRoleAsync(role.RoleName)));
        }

        return TypedResults.Ok(result.OrderBy(role => role.Name).ToArray());
    }
}

public sealed record RoleSummary(string Name, string Description, bool IsAdmin, bool IsSystem);
