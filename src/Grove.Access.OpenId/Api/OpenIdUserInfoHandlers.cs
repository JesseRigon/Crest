using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Grove.Api;
using Grove.Modules;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Microsoft.Extensions.Logging;
using Grove.Access.OpenId.Abstractions.Handlers;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grove.Access.OpenId.Api;

/// <summary>The userinfo endpoint (<c>/connect/userinfo</c>), mapped by <see cref="OpenIdProtocolApi"/>.</summary>
// Note: the error descriptions used in this handler are deliberately not localized as
// the OAuth 2.0 specification only allows select US-ASCII characters in error_description.
public sealed class OpenIdUserInfoHandlers : ApiHandlers
{
    private readonly IEnumerable<IUserInfoClaimsProvider> _claimsProviders;
    private readonly ILogger _logger;

    public OpenIdUserInfoHandlers(IEnumerable<IUserInfoClaimsProvider> claimsProviders, ILogger<OpenIdUserInfoHandlers> logger)
    {
        _claimsProviders = claimsProviders;
        _logger = logger;
    }

    // GET/POST: /connect/userinfo
    public async Task<IResult> MeAsync()
    {
        // Warning: this endpoint skips the API antiforgery check, which is required for this
        // stateless OpenID userinfo endpoint to work correctly.
        // To prevent effective CSRF/session fixation attacks, this endpoint MUST NOT return
        // an authentication cookie or try to establish an ASP.NET Core user session.

        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null)
        {
            return NotFound();
        }

        // Note: this endpoint authenticates here rather than through an authorization policy, so
        // it never throws if the OpenIddict server handler was not registered (e.g because the
        // OpenID server configuration was invalid).
        var principal = (await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme))?.Principal;
        if (principal == null)
        {
            return Results.Challenge(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Ensure the access token represents a user and not an application.
        var type = principal.FindFirst(OpenIdConstants.Claims.EntityType)?.Value;
        if (!string.Equals(type, OpenIdConstants.EntityTypes.User, StringComparison.Ordinal))
        {
            return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidRequest,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                    "The userinfo endpoint can only be used with access tokens representing users.",
            }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var claims = new Dictionary<string, object>();

        if (principal.HasScope(Scopes.Profile))
        {
            var preferredUsername = principal.FindFirst(Claims.PreferredUsername)?.Value;
            if (!string.IsNullOrEmpty(preferredUsername))
            {
                claims[Claims.PreferredUsername] = preferredUsername;
            }

            var name = principal.FindFirst(Claims.Name)?.Value ?? principal.FindFirst(ClaimTypes.Name)?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                claims[Claims.Name] = name;
            }

            var familyName = principal.FindFirst(Claims.FamilyName)?.Value ?? principal.FindFirst(ClaimTypes.Surname)?.Value;
            if (!string.IsNullOrEmpty(familyName))
            {
                claims[Claims.FamilyName] = familyName;
            }

            var givenName = principal.FindFirst(Claims.GivenName)?.Value ?? principal.FindFirst(ClaimTypes.GivenName)?.Value;
            if (!string.IsNullOrEmpty(givenName))
            {
                claims[Claims.GivenName] = givenName;
            }

            var middleName = principal.FindFirst(Claims.MiddleName)?.Value;
            if (!string.IsNullOrEmpty(middleName))
            {
                claims[Claims.MiddleName] = middleName;
            }

            var picture = principal.FindFirst(Claims.Picture)?.Value;
            if (!string.IsNullOrEmpty(picture))
            {
                claims[Claims.Picture] = picture;
            }

            var updatedAtClaimValue = principal.FindFirst(Claims.UpdatedAt)?.Value;
            if (!string.IsNullOrEmpty(updatedAtClaimValue))
            {
                claims[Claims.UpdatedAt] = long.Parse(updatedAtClaimValue, CultureInfo.InvariantCulture);
            }
        }

        // Note: the "sub" claim is a mandatory claim and must be included in the JSON response.
        claims[Claims.Subject] = principal.GetUserIdentifier();

        if (principal.HasScope(Scopes.Email))
        {
            var address = principal.FindFirst(Claims.Email)?.Value ?? principal.FindFirst(ClaimTypes.Email)?.Value;

            if (!string.IsNullOrEmpty(address))
            {
                claims[Claims.Email] = address;

                var status = principal.FindFirst(Claims.EmailVerified)?.Value;
                if (!string.IsNullOrEmpty(status))
                {
                    claims[Claims.EmailVerified] = bool.Parse(status);
                }
            }
        }

        if (principal.HasScope(Scopes.Phone))
        {
            var phone = principal.FindFirst(Claims.PhoneNumber)?.Value
                ?? principal.FindFirst(ClaimTypes.MobilePhone)?.Value
                ?? principal.FindFirst(ClaimTypes.HomePhone)?.Value
                ?? principal.FindFirst(ClaimTypes.OtherPhone)?.Value;

            if (!string.IsNullOrEmpty(phone))
            {
                claims[Claims.PhoneNumber] = phone;

                var status = principal.FindFirst(Claims.PhoneNumberVerified)?.Value;
                if (!string.IsNullOrEmpty(status))
                {
                    claims[Claims.PhoneNumberVerified] = bool.Parse(status);
                }
            }
        }

        if (principal.HasScope(Scopes.Roles))
        {
            var roles = principal.GetRoles();

            if (roles.Length != 0)
            {
                claims["roles"] = roles;
            }
        }

        // Note: the complete list of standard claims supported by the OpenID Connect specification
        // can be found here: http://openid.net/specs/openid-connect-core-1_0.html#StandardClaims

        var context = new UserInfoClaimsContext(principal, claims);
        await _claimsProviders.InvokeAsync((claimsProvider, ctx) => claimsProvider.GenerateAsync(ctx), context, _logger);

        return Ok(claims);
    }
}
