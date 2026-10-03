using Crest.Members.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Crest.Members.Services;

/// <summary>
/// Which login surface the current request is: tenant (default) or the member portal.
/// The portal's own endpoints mark the request explicitly; an external-login callback
/// carries the mark on the EXTERNAL-scheme cookie's properties, set by the portal's
/// external-login start (see <see cref="MemberSessionKeys.PortalOrganization"/>).
/// One scoped answer per request, read by the login-surface gate and the creation
/// stamp - the two places the surface decides what happens to an account.
/// </summary>
public sealed class MemberPortalLoginContext(IHttpContextAccessor httpContextAccessor)
{
    private PortalLoginMarker? _marker;
    private bool _resolved;

    /// <summary>Marks the current request as a portal login/registration.</summary>
    public void MarkPortal(string? organizationId, string? personId = null)
    {
        _marker = new PortalLoginMarker(organizationId, personId);
        _resolved = true;
    }

    /// <summary>Null on a tenant surface.</summary>
    public async Task<PortalLoginMarker?> ResolveAsync()
    {
        if (_resolved)
        {
            return _marker;
        }

        _resolved = true;
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var external = await httpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (external.Succeeded
            && external.Properties?.Items.TryGetValue(MemberSessionKeys.PortalOrganization, out var organizationId) == true
            && !string.IsNullOrWhiteSpace(organizationId))
        {
            _marker = new PortalLoginMarker(organizationId, null);
        }

        return _marker;
    }
}

/// <summary>The portal mark: which organization (null = "any of the member's
/// bindings", password sign-in only) and, for registration, the Person already
/// created for the account.</summary>
public sealed record PortalLoginMarker(string? OrganizationId, string? PersonId);
