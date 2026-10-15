namespace Grove.Themes.Members.Services;

/// <summary>The outcome of a member sign-in or registration attempt.</summary>
/// <remarks>
/// <paramref name="Error"/> is what the surface may show the caller. It is deliberately a
/// single opaque message rather than a structured reason: an unauthenticated caller does
/// not get to learn whether a user name exists, so the implementation must not hand back
/// anything that distinguishes "no such user" from "wrong password".
/// </remarks>
/// <para><paramref name="RequiresTwoFactor"/>: the password was right and the account has two-factor
/// authentication; the sign-in finishes with its second step (the sign-in API's two-factor
/// challenge), not yet signed in.</para>
public sealed record MemberAuthenticationResult(bool Succeeded, string? Error = null, bool RequiresTwoFactor = false)
{
    public static MemberAuthenticationResult Success { get; } = new(true);

    public static MemberAuthenticationResult TwoFactor { get; } = new(false, null, RequiresTwoFactor: true);

    public static MemberAuthenticationResult Failed(string error) => new(false, error);
}

/// <summary>
/// Member sign-in, registration and sign-out, as the member shell's own pages call them.
/// </summary>
/// <remarks>
/// Grove owns the member login <em>surface</em> - a page in the member shell, at the
/// member base, rendered InteractiveWebAssembly so the auth cookie reaches the browser.
/// It does not own what a member <em>is</em>: classes, organization bindings and
/// provisioning belong to <c>Grove.Access.Members</c>, whose member-shell library supplies the
/// implementation; MemberRoutes activates it once that library is loaded.
/// Upstream never references downstream, so this interface is the seam between them.
///
/// A host with the member theme but no Members feature gets
/// <see cref="UnavailableMemberAuthenticationService"/>: the surface renders and refuses,
/// rather than the shell failing to construct.
/// </remarks>
public interface IMemberAuthenticationService
{
    Task<MemberAuthenticationResult> SignInAsync(string userName, string password, bool rememberMe);

    /// <summary>
    /// Signs in with the headers the login form's sections contributed (a captcha's token, sent as
    /// the header the login events verify). An implementation without sections ignores them.
    /// </summary>
    Task<MemberAuthenticationResult> SignInAsync(string userName, string password, bool rememberMe, IReadOnlyDictionary<string, string?>? headers) =>
        SignInAsync(userName, password, rememberMe);

    Task<MemberAuthenticationResult> RegisterAsync(string userName, string email, string password, string firstName, string lastName, string? organizationId);

    Task SignOutAsync();

    /// <summary>The external identity providers the tenant offers on the portal.</summary>
    Task<IReadOnlyList<MemberExternalProvider>> GetExternalProvidersAsync();

    /// <summary>
    /// The URL that starts an external sign-in with <paramref name="provider"/> - a navigation,
    /// not a fetch: the challenge redirects to the provider. Relative to the member shell's base.
    /// </summary>
    string ExternalLoginUrl(string provider, string? organizationId, string returnUrl);

    /// <summary>Sends a password-reset link to the member portal. Always "succeeds": whether
    /// the account exists is not something an unauthenticated caller gets to learn.</summary>
    Task<MemberAuthenticationResult> RequestPasswordResetAsync(string userNameOrEmail);

    /// <summary>Sets a new password from the reset link's code.</summary>
    Task<MemberAuthenticationResult> ResetPasswordAsync(string userNameOrEmail, string code, string newPassword);
}

/// <summary>An external identity provider offered on the member portal.</summary>
public sealed record MemberExternalProvider(string Name, string DisplayName);

/// <summary>
/// The fallback when no module supplies member authentication.
/// </summary>
/// <remarks>
/// Refuses every attempt. This is the correct failure for a host that enabled the member
/// theme without the feature that implements members: the shell is reachable, the surface
/// renders, and signing in is impossible - rather than a missing-service exception at
/// first paint, or worse, a surface that appears to work.
/// </remarks>
public sealed class UnavailableMemberAuthenticationService : IMemberAuthenticationService
{
    private const string Unavailable = "Member accounts are not available on this site.";

    public Task<MemberAuthenticationResult> SignInAsync(string userName, string password, bool rememberMe) =>
        Task.FromResult(MemberAuthenticationResult.Failed(Unavailable));

    public Task<MemberAuthenticationResult> RegisterAsync(string userName, string email, string password, string firstName, string lastName, string? organizationId) =>
        Task.FromResult(MemberAuthenticationResult.Failed(Unavailable));

    public Task SignOutAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<MemberExternalProvider>> GetExternalProvidersAsync() =>
        Task.FromResult<IReadOnlyList<MemberExternalProvider>>([]);

    public string ExternalLoginUrl(string provider, string? organizationId, string returnUrl) => string.Empty;

    public Task<MemberAuthenticationResult> RequestPasswordResetAsync(string userNameOrEmail) =>
        Task.FromResult(MemberAuthenticationResult.Failed(Unavailable));

    public Task<MemberAuthenticationResult> ResetPasswordAsync(string userNameOrEmail, string code, string newPassword) =>
        Task.FromResult(MemberAuthenticationResult.Failed(Unavailable));
}
