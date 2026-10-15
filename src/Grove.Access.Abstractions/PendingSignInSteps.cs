using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Grove.Access;

/// <summary>
/// A step a signed-in user must take before the session may do anything else (setting up the
/// two-factor authentication the user's roles require). While one is pending the access gate
/// answers every request outside <see cref="AllowedPaths"/>: a page with a redirect to
/// <see cref="Page"/>, an API call with 403 and a problem body naming <see cref="Reason"/>.
/// </summary>
/// <param name="Reason">The stable reason code, the problem body's <c>reason</c> ("two-factor-setup-required").</param>
/// <param name="Detail">What the user must do, for the problem body.</param>
/// <param name="Page">The tenant-relative page that takes the step ("/TwoFactor").</param>
/// <param name="AllowedPaths">Tenant-relative path prefixes the session may still reach: the step's own pages and APIs.</param>
public sealed record PendingSignInStep(string Reason, string Detail, string Page, IReadOnlyList<string> AllowedPaths);

/// <summary>
/// Says whether a signed-in user has a <see cref="PendingSignInStep"/>. Asked by the access gate
/// once per request for an authenticated cookie session; registered by the feature that owns the
/// step (Grove.Users' two-factor authentication).
/// </summary>
public interface IPendingSignInStepProvider
{
    Task<PendingSignInStep?> GetAsync(HttpContext context, ClaimsPrincipal user);
}

/// <summary>What the gate lets through whatever step is pending.</summary>
public static class PendingSignInSteps
{
    /// <summary>
    /// Always reachable: the shell runtime (the framework files, the circuit, the static web
    /// assets), the routing options and the client's strings the documents boot with, the
    /// antiforgery token, signing in and out, and the error pages.
    /// </summary>
    public static readonly IReadOnlyList<string> AlwaysAllowed =
    [
        "/_framework",
        "/_content",
        "/_blazor",
        "/api/grove/routing",
        "/api/grove/localization",
        "/api/grove/antiforgery",
        "/api/grove/auth",
        "/Error",
    ];

    /// <summary>Whether <paramref name="path"/> (tenant-relative, or a shell's infrastructure path) is under one of <paramref name="prefixes"/>.</summary>
    public static bool Allows(PathString path, IEnumerable<string> prefixes) =>
        prefixes.Any(prefix => path.StartsWithSegments(new PathString(prefix), StringComparison.OrdinalIgnoreCase));
}
