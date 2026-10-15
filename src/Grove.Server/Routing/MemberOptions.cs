using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Grove.Environment.Shell.Configuration;

namespace Grove.Routing;

/// <summary>
/// Binds <see cref="MemberOptions"/> from the tenant's shell configuration, so a recipe or
/// appsettings section can move the member shell the way it can move the admin one.
/// </summary>
/// <remarks>
/// Grove exposes a tenant's configuration as <see cref="IShellConfiguration"/>; binding
/// a section of it is how <c>AdminOptions</c> itself is configured
/// (<c>Grove.Admin</c>'s own startup binds "Grove_Admin"). Grove's section is
/// named for Grove rather than borrowing an Grove one, because this is Grove's option
/// and not an Grove setting Grove happens to read.
/// </remarks>
public sealed class MemberOptionsConfiguration(IShellConfiguration shellConfiguration) : IConfigureOptions<MemberOptions>
{
    public void Configure(MemberOptions options)
    {
        shellConfiguration.GetSection("Grove_Member").Bind(options);

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
