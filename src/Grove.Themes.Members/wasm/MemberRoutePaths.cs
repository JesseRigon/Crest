namespace Grove.Themes.Members;

/// <summary>
/// The member shell's page routes.
/// </summary>
/// <remarks>
/// These constants ARE the registration point: each page declares its route as
/// <c>@attribute [Route(MemberRoutePaths.X)]</c> off this one literal, and navigation between
/// the pages resolves the same value instead of re-typing it.
///
/// Pages marked <c>[OrganizationFreePage]</c> (sign-in, registration, password reset, the
/// picker) are served at the member base; every other page under an organization,
/// <c>/{member prefix}/{organization slug}/…</c>, so their routes are relative to that.
///
/// They are <strong>shell-relative</strong>, and carry no "/members" prefix. The
/// middleware shifts the member base (<c>MemberOptions.MemberUrlPrefix</c>, tenant-settable)
/// into <c>PathBase</c> before endpoint routing runs, so the route table matches the bare
/// literal while every URL the browser sees carries the tenant prefix plus the member
/// base. Writing the prefix in here would hardcode the one thing the option exists to make
/// configurable - a tenant that moved its member shell would get links to the old path.
/// </remarks>
public static class MemberRoutePaths
{
    /// <summary>The member's landing page.</summary>
    public const string Home = "/";

    /// <summary>Member sign-in. Organization-free, and an auth-cookie page
    /// (<c>[SetsAuthenticationCookie]</c>: renders InteractiveWebAssembly).</summary>
    public const string Login = "/login";

    /// <summary>Member self-registration. Also an auth-cookie page.</summary>
    public const string Register = "/register";

    /// <summary>The member's own account: profile, credentials, organizations.</summary>
    public const string Account = "/account";

    /// <summary>The member's drives: their organization's drive and what is shared with them
    /// (docs/media.md § 8).</summary>
    public const string Files = "/files";

    /// <summary>Shown for a member URL with no member page. Organization-free.</summary>
    public const string NotFound = "/not-found";

    /// <summary>Requests a password-reset link. Organization-free.</summary>
    public const string ForgotPassword = "/forgot-password";

    /// <summary>Sets a new password from the emailed link's code. Organization-free.</summary>
    public const string ResetPassword = "/reset-password";

    /// <summary>Sign-in with an external identity provider, and where a refused external
    /// sign-in comes back to. Organization-free.</summary>
    public const string ExternalLogin = "/external-login";

    /// <summary>The organization picker: where a member who has not named an organization
    /// lands. Organization-free.</summary>
    public const string Organizations = "/organizations";
}
