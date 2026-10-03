using System.Security.Claims;
using Crest.Members.Constants;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Security;

namespace Crest.Members.Services;

/// <summary>
/// Per-request session enrichment (design: plans/user-systems.md §D): CHAINS onto the
/// application cookie's OnValidatePrincipal - never replaces it, because the inner
/// delegate is Identity's SecurityStampValidator and dropping it would silently
/// disable stamp validation. For member sessions it contributes the ACTIVE org claim
/// plus that binding's role-template claims (role name + the role's "Permission"
/// claims, exactly what stock RoleClaimsProvider bakes at sign-in), and for
/// impersonation sessions the impersonator claim - so BOTH identities ride the
/// principal (the dual-attribution prerequisite). Enrichment never renews the cookie:
/// claims are per-request, rebuilt fresh from session items each time.
/// </summary>
public class MemberCookieEventsConfiguration : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    public void Configure(string? name, CookieAuthenticationOptions options)
    {
        if (name != IdentityConstants.ApplicationScheme)
        {
            return;
        }

        var inner = options.Events.OnValidatePrincipal;
        options.Events.OnValidatePrincipal = async context =>
        {
            if (inner is not null)
            {
                await inner(context);
            }

            if (context.Principal?.Identity?.IsAuthenticated == true)
            {
                await EnrichAsync(context);
            }
        };
    }

    public void Configure(CookieAuthenticationOptions options)
    {
    }

    private static async Task EnrichAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal!;
        var items = context.Properties.Items;

        items.TryGetValue(MemberSessionKeys.ActiveOrganization, out var activeOrgId);
        items.TryGetValue(MemberSessionKeys.ImpersonatorUserId, out var impersonatorId);

        if (activeOrgId is null && impersonatorId is null)
        {
            return;
        }

        // Idempotence: a re-entrant validation (or a principal that was re-signed
        // before the strip existed) must not stack duplicates.
        var clone = MemberSessionService.StripEnrichment(principal);
        var identity = clone.Identities.First();

        if (impersonatorId is not null)
        {
            identity.AddClaim(new Claim(MemberClaims.Impersonator, impersonatorId));
        }

        if (activeOrgId is not null && principal.FindFirst(MemberClaims.UserClass)?.Value == UserClasses.Member)
        {
            var services = context.HttpContext.RequestServices;
            var memberService = services.GetRequiredService<IMemberService>();
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var member = userId is null ? null : await memberService.GetAsync(userId);
            var binding = member?.Bindings.FirstOrDefault(b => b.OrganizationId == activeOrgId);
            if (binding is not null)
            {
                identity.AddClaim(new Claim(MemberClaims.ActiveOrganization, activeOrgId));

                var roleManager = services.GetRequiredService<RoleManager<IRole>>();
                var identityOptions = services.GetRequiredService<IOptions<IdentityOptions>>().Value;
                foreach (var roleName in binding.Roles)
                {
                    var role = await roleManager.FindByNameAsync(roleName);
                    if (role is null)
                    {
                        continue;
                    }

                    identity.AddClaim(new Claim(identityOptions.ClaimsIdentity.RoleClaimType, role.RoleName));
                    foreach (var claim in await roleManager.GetClaimsAsync(role))
                    {
                        identity.AddClaim(claim);
                    }
                }
            }
        }

        context.ReplacePrincipal(clone);
    }
}
