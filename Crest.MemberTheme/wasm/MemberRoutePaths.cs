namespace Crest.MemberTheme;

/// <summary>
/// The member shell's page routes.
/// </summary>
/// <remarks>
/// These constants ARE the registration point: each page declares its route as
/// <c>@attribute [Route(MemberRoutePaths.X)]</c> off this one literal, and navigation between
/// the pages resolves the same value instead of re-typing it.
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

    /// <summary>Member sign-in. Renders InteractiveWebAssembly: see App.razor's
    /// auth-cookie render-mode rule.</summary>
    public const string Login = "/login";

    /// <summary>Member self-registration. Also an auth-cookie page.</summary>
    public const string Register = "/register";

    /// <summary>The member's own account: profile, credentials, organizations.</summary>
    public const string Account = "/account";

    /// <summary>Shown for a member URL with no member page.</summary>
    public const string NotFound = "/not-found";
}
