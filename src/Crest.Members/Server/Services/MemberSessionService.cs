using System.Security.Claims;
using Crest.Members.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Crest.Members.Services;

/// <summary>
/// Session state for member sessions: the ACTIVE organization and (for impersonation)
/// the staff identity, carried in AuthenticationProperties.Items on the ONE per-tenant
/// Identity cookie (design ruling: no second cookie scheme - SignInManager is
/// hard-wired to the stock scheme, and a parallel cookie would reimplement 2FA,
/// lockout and stamp handling). Server-side when a ticket store is active.
/// </summary>
public class MemberSessionService
{
    /// <summary>Claim types this module contributes per request; stripped before any
    /// re-issue so re-signing an enriched principal never bakes stale claims into the
    /// cookie.</summary>
    internal static readonly string[] EnrichmentClaimTypes =
    [
        MemberClaims.ActiveOrganization,
        MemberClaims.Impersonator,
    ];

    public async Task<string?> GetActiveOrganizationAsync(HttpContext httpContext)
    {
        var result = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        return result.Properties?.Items.TryGetValue(MemberSessionKeys.ActiveOrganization, out var orgId) == true ? orgId : null;
    }

    public async Task<string?> GetImpersonatorAsync(HttpContext httpContext)
    {
        var result = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        return result.Properties?.Items.TryGetValue(MemberSessionKeys.ImpersonatorUserId, out var userId) == true ? userId : null;
    }

    /// <summary>Re-issues the session with a new active organization. The caller has
    /// already validated the binding. The principal is re-signed WITHOUT this module's
    /// enrichment claims - the next request's enrichment rebuilds them for the new org.</summary>
    public async Task SetActiveOrganizationAsync(HttpContext httpContext, string organizationId)
    {
        var result = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (result.Principal is null)
        {
            throw new InvalidOperationException("No authenticated session to update.");
        }

        var properties = result.Properties ?? new AuthenticationProperties();
        properties.Items[MemberSessionKeys.ActiveOrganization] = organizationId;

        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, StripEnrichment(result.Principal), properties);
    }

    internal static ClaimsPrincipal StripEnrichment(ClaimsPrincipal principal)
    {
        var clone = principal.Clone();
        foreach (var identity in clone.Identities)
        {
            foreach (var claimType in EnrichmentClaimTypes)
            {
                foreach (var claim in identity.FindAll(claimType).ToArray())
                {
                    identity.RemoveClaim(claim);
                }
            }
        }

        return clone;
    }
}
