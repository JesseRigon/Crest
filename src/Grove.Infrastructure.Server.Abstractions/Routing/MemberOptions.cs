namespace Grove.Routing;

/// <summary>
/// Where the member shell lives, and what it is called.
/// </summary>
/// <remarks>
/// Shaped after Grove's own <c>AdminOptions.AdminUrlPrefix</c>: one option, bound from
/// the tenant's shell configuration, read through <c>IOptions&lt;MemberOptions&gt;</c> and
/// never re-typed as a literal at a call site. Every member URL - navigation, the login
/// surface, cross-shell links - is composed from <see cref="MemberUrlPrefix"/>, so a
/// tenant that changes the prefix gets working links with no further edits, exactly the
/// way <c>AdminUrlPrefix "backoffice"</c> already works for the admin shell.
///
/// The override is part of the feature rather than a later addition: a default nobody can
/// change is not a tenant setting, and a path that only works at its default value is the
/// bug this option exists to prevent.
/// </remarks>
public sealed class MemberOptions
{
    /// <summary>
    /// The member shell's base path, without leading or trailing slashes ("members" by
    /// default). Bound from the <c>Grove_Member:MemberUrlPrefix</c> shell configuration
    /// key.
    /// </summary>
    public string MemberUrlPrefix { get; set; } = "members";
}
