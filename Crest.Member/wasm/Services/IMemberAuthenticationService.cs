namespace Crest.Member.Services;

/// <summary>The outcome of a member sign-in or registration attempt.</summary>
/// <remarks>
/// <paramref name="Error"/> is what the surface may show the caller. It is deliberately a
/// single opaque message rather than a structured reason: an unauthenticated caller does
/// not get to learn whether a user name exists, so the implementation must not hand back
/// anything that distinguishes "no such user" from "wrong password".
/// </remarks>
public sealed record MemberAuthenticationResult(bool Succeeded, string? Error = null)
{
    public static MemberAuthenticationResult Success { get; } = new(true);

    public static MemberAuthenticationResult Failed(string error) => new(false, error);
}

/// <summary>
/// Member sign-in, registration and sign-out, as the member shell's own pages call them.
/// </summary>
/// <remarks>
/// Crest owns the member login <em>surface</em> - a page in the member shell, at the
/// member base, rendered InteractiveWebAssembly so the auth cookie reaches the browser.
/// It does not own what a member <em>is</em>: classes, organization bindings and
/// provisioning belong to <c>Crest.Members</c>, whose member-shell library supplies the
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

    Task<MemberAuthenticationResult> RegisterAsync(string userName, string email, string password, string? organizationId);

    Task SignOutAsync();
}

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

    public Task<MemberAuthenticationResult> RegisterAsync(string userName, string email, string password, string? organizationId) =>
        Task.FromResult(MemberAuthenticationResult.Failed(Unavailable));

    public Task SignOutAsync() => Task.CompletedTask;
}
