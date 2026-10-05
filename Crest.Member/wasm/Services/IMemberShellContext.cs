namespace Crest.Member.Services;

/// <summary>One organization the signed-in member is bound to, as the shell displays it.</summary>
public sealed record MemberShellOrganization(string Id, string Name);

/// <summary>One entry in the member shell's navigation region.</summary>
public sealed record MemberShellNavigationEntry(string Path, string Text, int Position = 0);

/// <summary>
/// Everything the member shell's chrome needs, supplied by whichever module owns what a
/// member <em>is</em>.
/// </summary>
/// <remarks>
/// This is the seam that keeps the member shell generic. Crest owns the shell - the base
/// path, the document, the route bucket, the layout, the login surface - while
/// <c>Crest.Members</c> owns sessions, organization bindings, member classes and
/// provisioning. The shell therefore asks for a name, a list of organizations and a set of
/// nav entries; it never knows that an organization is a party content item or that a
/// member has a class, and Crest never references the Members module. Upstream does not
/// reference downstream: the module injects its implementation upward.
///
/// A host with no implementation registered still gets a working shell: the default
/// implementation below renders chrome with no organizations and no nav, which is what an
/// anonymous or non-member visitor should see.
///
/// <para>
/// Nothing exposed here is an authorization decision. The shell is one theme shared by
/// every organization-bound user in the tenant, so what it renders must already be
/// scoped by the server for this member. Hiding a nav entry is presentation; the
/// permission lives in the query that reads the records, keyed on the active organization
/// and the member's class, and fails closed.
/// </para>
/// </remarks>
public interface IMemberShellContext
{
    /// <summary>The site or product name shown in the shell header.</summary>
    string SiteName { get; }

    /// <summary>The signed-in member's display name.</summary>
    string UserName { get; }

    /// <summary>Every organization this member is bound to.</summary>
    IReadOnlyList<MemberShellOrganization> Organizations { get; }

    /// <summary>The organization this session is currently acting in.</summary>
    string? ActiveOrganizationId { get; }

    /// <summary>The active organization's display name.</summary>
    string? ActiveOrganizationName { get; }

    /// <summary>The member nav entries contributed for this member.</summary>
    IReadOnlyList<MemberShellNavigationEntry> NavigationEntries { get; }

    /// <summary>Shell-relative path of the member's own account page.</summary>
    string AccountPath { get; }

    /// <summary>Localized caption for the sign-out control.</summary>
    string SignOutText { get; }

    /// <summary>Raised when the shell's state changed and the chrome should re-render.</summary>
    event Action? Changed;

    /// <summary>Loads the shell's state if it has not been loaded for this session yet.</summary>
    Task EnsureLoadedAsync();

    /// <summary>Switches the organization this session acts in.</summary>
    Task SetActiveOrganizationAsync(string organizationId);

    /// <summary>Ends the member's session.</summary>
    Task SignOutAsync();
}

/// <summary>
/// The member shell with no module supplying its state: chrome, no organizations, no nav.
/// </summary>
/// <remarks>
/// What MemberRoutes uses when no member module supplies an implementation, so the member
/// theme is self-sufficient. A Crest host that
/// enables the member theme without the Members feature gets a shell that renders rather
/// than a null-reference at first paint; an anonymous visitor reaching a member page sees
/// the same. It deliberately does nothing on switch or sign-out: with no module there is
/// no session to act on.
/// </remarks>
public sealed class EmptyMemberShellContext : IMemberShellContext
{
    public string SiteName { get; } = string.Empty;

    public string UserName { get; } = string.Empty;

    public IReadOnlyList<MemberShellOrganization> Organizations { get; } = [];

    public string? ActiveOrganizationId => null;

    public string? ActiveOrganizationName => null;

    public IReadOnlyList<MemberShellNavigationEntry> NavigationEntries { get; } = [];

    public string AccountPath { get; } = "/account";

    public string SignOutText { get; } = "Sign out";

    // Never raised: this context has no state to change. Declared because the interface
    // requires it, so the layout can subscribe unconditionally.
#pragma warning disable CS0067 // The event is never used
    public event Action? Changed;
#pragma warning restore CS0067

    public Task EnsureLoadedAsync() => Task.CompletedTask;

    public Task SetActiveOrganizationAsync(string organizationId) => Task.CompletedTask;

    public Task SignOutAsync() => Task.CompletedTask;
}
