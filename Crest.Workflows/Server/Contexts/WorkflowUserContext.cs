using System.Security.Claims;

namespace Crest.Workflows.Contexts;

/// <summary>
/// The acting user at the moment a workflow was triggered, carried as workflow input under
/// <see cref="InputKey"/>. Activities run on a background job with no HttpContext, so the
/// principal is snapshotted as its claims (type + value) and rebuilt on demand: Orchard's
/// authorization pipeline - role permissions, the super user, the member class
/// ceiling (which reads the class claim), the active-organization claim - then evaluates
/// exactly as it would have in the request. Tenant name is included for logs and journals
/// only; the engine itself is per shell, so no activity can address another tenant's store.
/// </summary>
public sealed class WorkflowUserContext
{
    public const string InputKey = WorkflowsConstants.InputKeys.Actor;

    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public bool IsAuthenticated { get; set; }
    public string? AuthenticationType { get; set; }
    public string Tenant { get; set; } = string.Empty;
    public List<WorkflowUserClaim> Claims { get; set; } = [];

    public static WorkflowUserContext Anonymous(string tenant) => new() { Tenant = tenant };

    public static WorkflowUserContext From(ClaimsPrincipal? principal, string tenant)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Anonymous(tenant);
        }

        return new()
        {
            IsAuthenticated = true,
            UserName = principal.Identity.Name,
            UserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            AuthenticationType = principal.Identity.AuthenticationType,
            Tenant = tenant,
            Claims = principal.Claims.Select(claim => new WorkflowUserClaim(claim.Type, claim.Value)).ToList(),
        };
    }

    public ClaimsPrincipal ToPrincipal()
    {
        if (!IsAuthenticated)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var identity = new ClaimsIdentity(
            Claims.Select(claim => new Claim(claim.Type, claim.Value)),
            AuthenticationType ?? "CrestWorkflows",
            ClaimTypes.Name,
            ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}

public sealed record WorkflowUserClaim(string Type, string Value);
