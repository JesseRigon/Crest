using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace Crest.Routing;

/// <summary>
/// Where the member shell lives, and what it is called.
/// </summary>
/// <remarks>
/// Shaped after Orchard's own <c>AdminOptions.AdminUrlPrefix</c>: one option, bound from
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
    /// default). Bound from the <c>Crest_Member:MemberUrlPrefix</c> shell configuration
    /// key.
    /// </summary>
    public string MemberUrlPrefix { get; set; } = "members";

    /// <summary>
    /// The theme id that marks a theme as Crest's member shell, the member-bucket
    /// counterpart to <c>BlazorAdminThemeOptions.BlazorAdminThemeId</c>.
    /// </summary>
    public string MemberThemeId { get; set; } = "OrchardCore.Crest.Member";
}

/// <summary>
/// Binds <see cref="MemberOptions"/> from the tenant's shell configuration, so a recipe or
/// appsettings section can move the member shell the way it can move the admin one.
/// </summary>
/// <remarks>
/// Orchard exposes a tenant's configuration as <see cref="IShellConfiguration"/>; binding
/// a section of it is how <c>AdminOptions</c> itself is configured
/// (<c>OrchardCore.Admin</c>'s own startup binds "OrchardCore_Admin"). Crest's section is
/// named for Crest rather than borrowing an Orchard one, because this is Crest's option
/// and not an Orchard setting Crest happens to read.
/// </remarks>
public sealed class MemberOptionsConfiguration(IShellConfiguration shellConfiguration) : IConfigureOptions<MemberOptions>
{
    public void Configure(MemberOptions options)
    {
        shellConfiguration.GetSection("Crest_Member").Bind(options);

        // A prefix is a path segment, not a path: the middleware composes "/" + prefix,
        // and a stray slash either doubles the separator or silently produces a
        // shell base that no longer matches the routes built from it.
        options.MemberUrlPrefix = options.MemberUrlPrefix?.Trim('/') ?? string.Empty;

        // An empty prefix would put the member shell at the tenant root, where the site
        // shell already lives as the fallback bucket - two shells claiming "/" with the
        // gate unable to tell them apart. Fall back to the default rather than serve a
        // tenant that cannot route.
        if (string.IsNullOrWhiteSpace(options.MemberUrlPrefix))
        {
            options.MemberUrlPrefix = "members";
        }
    }
}
