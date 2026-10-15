#nullable enable

using System.Security.Claims;
using Grove.Access;
using Grove.Users.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Grove.Users.Services;

/// <summary>
/// The sign-in step of a user whose roles require two-factor authentication and who has not set
/// it up: until they do, the session reaches the two-factor page and what it needs (its API, the
/// email confirmation for the email method) and nothing else - enforced by the access gate.
/// </summary>
public sealed class TwoFactorSetupStep(UserManager<IUser> userManager, ITwoFactorAuthenticationHandlerCoordinator coordinator) : IPendingSignInStepProvider
{
    public const string Reason = "two-factor-setup-required";

    /// <summary>The pages and APIs a session with the step pending may reach, tenant-relative prefixes.</summary>
    public static readonly IReadOnlyList<string> AllowedPaths =
    [
        AccountPages.TwoFactor,
        "/Authenticator",
        "/GenerateRecoveryCodes",
        "/ShowRecoveryCodes",
        "/DisableTwoFactorAuthentication",
        AccountPages.ConfirmEmail,
        AccountPages.ConfirmEmailSent,
        "/api/grove/two-factor",
        "/api/grove/account/email",
    ];

    public async Task<PendingSignInStep?> GetAsync(HttpContext context, ClaimsPrincipal user)
    {
        // The claim the sign-in stamped is the hint; the account decides (it may have been set
        // up since, in this session).
        if (!user.HasClaim(claim => claim.Type == UserConstants.TwoFactorAuthenticationClaimType)
            || await userManager.GetUserAsync(user) is not { } account
            || await userManager.GetTwoFactorEnabledAsync(account)
            || !await coordinator.IsRequiredAsync(account))
        {
            return null;
        }

        return new PendingSignInStep(
            Reason,
            "Two-factor authentication must be enabled before proceeding.",
            AccountPages.TwoFactor,
            AllowedPaths);
    }
}
