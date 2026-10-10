using System.Security.Claims;
using Crest.Access;

namespace Crest.Workflows.Contexts;

/// <summary>
/// The actor of a workflow run: who started it, carried as workflow input under
/// <see cref="InputKey"/> and persisted with the instance. Identity and shell only, never
/// rights: the caller is built again from this identity at the start of every burst
/// (docs/operations.md › Caller lifetime), so a right lost between bursts fails the next
/// one, and nothing serialized into an instance can grant anything. <see cref="IsSystem"/>
/// records that the system was acting when the run was captured; it never produces the
/// system caller on its own (only a definition published as system does).
/// </summary>
public sealed class WorkflowUserContext
{
    public const string InputKey = WorkflowsConstants.InputKeys.Actor;

    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Tenant { get; set; } = string.Empty;
    public CallerSide Side { get; set; }
    public string? OrganizationId { get; set; }
    public bool IsSystem { get; set; }

    public bool IsAuthenticated => UserId is not null;

    public static WorkflowUserContext Anonymous(string tenant, CallerSide side = CallerSide.Site) => new() { Tenant = tenant, Side = side };

    /// <summary>The identity of a built caller: what the gate set for the request or burst that captures this actor.</summary>
    public static WorkflowUserContext From(CallerContext caller) => new()
    {
        UserId = caller.UserId,
        UserName = caller.UserName,
        Tenant = caller.Tenant,
        Side = caller.Side,
        OrganizationId = caller.OrganizationId,
        IsSystem = caller.IsSystem,
    };

    /// <summary>The identity of an authenticated principal (its name identifier and name), or the anonymous actor.</summary>
    public static WorkflowUserContext From(ClaimsPrincipal? principal, string tenant, CallerSide side = CallerSide.Site)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Anonymous(tenant, side);
        }

        return new()
        {
            UserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.Identity.Name,
            UserName = principal.Identity.Name,
            Tenant = tenant,
            Side = side,
        };
    }

    /// <summary>
    /// Identity only, for <see cref="ICallerContextFactory.CreateAsync"/>: the factory reads
    /// the name identifier and the name and builds the rights from the server-side state.
    /// </summary>
    public ClaimsPrincipal ToIdentityPrincipal()
    {
        if (!IsAuthenticated)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, UserId!) };
        if (UserName is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, UserName));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "CrestWorkflows", ClaimTypes.Name, ClaimTypes.Role));
    }
}
