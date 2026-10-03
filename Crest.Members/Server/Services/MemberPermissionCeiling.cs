using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Crest.Members.Constants;
using OrchardCore.Security;

namespace Crest.Members.Services;

/// <summary>
/// The registry of permissions that are NEVER valid for member-class principals
/// (design: plans/user-systems.md §E). Name-based on purpose: Permission equality is
/// reference-based upstream, and dynamic permissions exist only as formatted names.
/// Modules contribute their staff-only permissions via
/// <c>services.Configure&lt;MemberPermissionCeilingOptions&gt;(...)</c> alongside their
/// normal IPermissionProvider. Prefixes cover the dynamic expansions
/// (ManageUsersInRole_{role}, …) so the ceiling stays CLOSED under the derived
/// permissions Orchard's handlers re-enter authorization with.
/// </summary>
public class MemberPermissionCeilingOptions
{
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _namePrefixes = [];

    public MemberPermissionCeilingOptions Ceiling(params string[] permissionNames)
    {
        foreach (var name in permissionNames)
        {
            _names.Add(name);
        }

        return this;
    }

    public MemberPermissionCeilingOptions CeilingPrefix(params string[] namePrefixes)
    {
        _namePrefixes.AddRange(namePrefixes);
        return this;
    }

    public bool IsCeilinged(string permissionName)
    {
        if (_names.Contains(permissionName))
        {
            return true;
        }

        foreach (var prefix in _namePrefixes)
        {
            if (permissionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// The enforcement point. A RAW IAuthorizationHandler (the SuperUserHandler shape),
/// deliberately WITHOUT the HasSucceeded early-out every stock handler uses: stock
/// handlers are additive-only, this one must run DESPITE prior successes. Why Fail()
/// and not claims curation (all verified upstream): admins carry ZERO permission
/// claims (SuperUserHandler blanket-succeeds on the admin role name), stamp refresh
/// copies claims additively (stale claims survive), and Anonymous/Authenticated role
/// claims are applied live per request - none of those paths can be ceilinged by
/// filtering claims. HasFailed is sticky and evaluation is !HasFailed &amp;&amp;
/// HasSucceeded, so this veto beats every Succeed regardless of handler order.
/// </summary>
public class MemberPermissionCeilingHandler : IAuthorizationHandler
{
    private readonly MemberPermissionCeilingOptions _options;

    public MemberPermissionCeilingHandler(IOptions<MemberPermissionCeilingOptions> options)
    {
        _options = options.Value;
    }

    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Sessions without the class claim are pre-feature staff sessions: member
        // sessions always carry the claim (the claims provider registers with the
        // feature that makes member creation possible at all).
        var userClass = context.User.FindFirst(MemberClaims.UserClass)?.Value;
        if (userClass != UserClasses.Member)
        {
            return Task.CompletedTask;
        }

        foreach (var requirement in context.Requirements.OfType<PermissionRequirement>())
        {
            if (_options.IsCeilinged(requirement.Permission.Name))
            {
                context.Fail(new AuthorizationFailureReason(
                    this,
                    $"Permission '{requirement.Permission.Name}' can never be granted to a member-class user."));
            }
        }

        return Task.CompletedTask;
    }
}
