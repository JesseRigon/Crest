using Grove.Access;
using Grove.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Routing;

/// <summary>
/// One registered shell entry: a base path the shell selector claims, and everything the
/// request pipeline needs to serve it - the shell (route bucket and document), the caller side,
/// the theme check, the authentication policy and the page an unmatched URL renders.
/// </summary>
/// <remarks>
/// A shell may have several entries: the admin shell is served at the admin prefix and, for
/// its login page, at the login path. The site shell has none - it is the fallback for every
/// request no entry claims (docs/shells-and-themes.md › A registry of shells).
/// </remarks>
public sealed class ShellDescriptor
{
    /// <summary>A name for logs and diagnostics ("admin", "login", "member").</summary>
    public required string Name { get; init; }

    /// <summary>The shell whose route table, document and endpoints serve this entry.</summary>
    public required ShellId Shell { get; init; }

    /// <summary>The side every request through this entry acts on.</summary>
    public required CallerSide Side { get; init; }

    /// <summary>The base path, tenant-relative, with a leading slash ("/Admin").</summary>
    public required string BasePath { get; init; }

    /// <summary>
    /// When set, every page URL under the base renders this one page (the admin login: a
    /// tenant-configured login path served by the login page's own <c>@page</c> literal).
    /// </summary>
    public string? FixedPage { get; init; }

    /// <summary>
    /// Whether the tenant's active theme serves this entry; null serves it unconditionally. A
    /// request the check declines is left to the rest of the pipeline (Grove's own MVC admin).
    /// </summary>
    public Func<IServiceProvider, Task<bool>>? ThemeDetector { get; init; }

    /// <summary>
    /// Where an unauthenticated request for a page that does not allow anonymous access is sent,
    /// tenant-relative; null when the entry requires no authentication of its own.
    /// </summary>
    public string? LoginPath { get; init; }

    /// <summary>Whether a page's route is authorized against the caller (the admin route gate).</summary>
    public bool AuthorizesRoutes { get; init; }

    /// <summary>The shell-relative page a URL with no page renders.</summary>
    public required string NotFoundPage { get; init; }

    /// <summary>
    /// Whether pages carry the organization in their path, <c>{base}/{slug}/…</c> (the member
    /// shell). Pages marked <c>[OrganizationFreePage]</c> are served at the base instead.
    /// </summary>
    public bool OrganizationInPath { get; init; }

    /// <summary>
    /// Shell-relative page a bare base is redirected to when <see cref="OrganizationInPath"/>
    /// is set and no organization is named (the organization picker).
    /// </summary>
    public string? OrganizationPicker { get; init; }
}

/// <summary>The registered shells; hosts add their own with <c>services.Configure</c>.</summary>
public sealed class ShellRegistryOptions
{
    public List<ShellDescriptor> Shells { get; } = [];
}

/// <summary>What the selector chose: the entry and the request path after its base.</summary>
public sealed record ShellSelection(ShellDescriptor Descriptor, PathString Remainder);

/// <summary>
/// Chooses the shell entry serving a request, or none (the site, the fallback).
/// </summary>
/// <remarks>
/// Takes the request rather than a path, so other matching rules - a shell on its own
/// hostname - can be added later without reshaping the selector. Path prefixes are the only
/// rule implemented: the longest registered base the path starts with wins.
/// </remarks>
public interface IShellSelector
{
    ShellSelection? Select(HttpRequest request);

    /// <summary>Every registered entry (the infrastructure strip and the routing API read them).</summary>
    IReadOnlyList<ShellDescriptor> Shells { get; }
}

public sealed class PathPrefixShellSelector(IOptions<ShellRegistryOptions> options) : IShellSelector
{
    private ShellDescriptor[]? _ordered;

    public IReadOnlyList<ShellDescriptor> Shells => _ordered ??= [.. options.Value.Shells.OrderByDescending(shell => shell.BasePath.Length)];

    public ShellSelection? Select(HttpRequest request)
    {
        foreach (var shell in Shells)
        {
            if (request.Path.StartsWithSegments(new PathString(shell.BasePath), StringComparison.OrdinalIgnoreCase, out var remainder))
            {
                return new ShellSelection(shell, remainder);
            }
        }

        return null;
    }
}

/// <summary>
/// Resolves the organization slug of a member page path for the signed-in user.
/// </summary>
/// <remarks>
/// Implemented by the module that owns what an organization binding is (Grove.Members);
/// Grove only knows that member pages carry one. A slug resolves only among the user's own
/// bindings, so a path can never name an organization the user may not act for: null is the
/// answer for anything else, and the page is not found.
/// </remarks>
public interface IShellOrganizationResolver
{
    Task<string?> ResolveAsync(HttpContext context, string slug);

    /// <summary>
    /// The signed-in user's organizations on this tenant (their member bindings), with the slug
    /// each one's member pages are addressed by - what the shell switcher lists. Empty for an
    /// anonymous or staff-only user.
    /// </summary>
    Task<IReadOnlyList<ShellOrganization>> ListAsync(HttpContext context);
}

/// <summary>An organization the signed-in user is bound to, as the shells display it.</summary>
public sealed record ShellOrganization(string Id, string Name, string Slug);

/// <summary>
/// Registers Grove's own shells from the tenant's configured paths: admin and its login page
/// (<see cref="BlazorAdminThemeOptions"/>) and member (<see cref="MemberOptions"/>). Every tenant
/// reserves the member prefix, whether or not Members is enabled.
/// </summary>
public sealed class ShellRegistryConfiguration(
    IOptions<BlazorAdminThemeOptions> adminOptions,
    IOptions<MemberOptions> memberOptions) : IConfigureOptions<ShellRegistryOptions>
{
    public const string LegacyHostPage = "/legacy-host";
    public const string MemberLoginPage = Grove.MemberTheme.MemberRoutePaths.Login;
    public const string MemberNotFoundPage = Grove.MemberTheme.MemberRoutePaths.NotFound;
    public const string MemberOrganizationPicker = Grove.MemberTheme.MemberRoutePaths.Organizations;

    public void Configure(ShellRegistryOptions options)
    {
        var admin = adminOptions.Value;
        static Task<bool> BlazorAdminTheme(IServiceProvider services) =>
            services.GetRequiredService<IBlazorAdminThemeDetector>().IsBlazorAdminThemeActiveAsync();

        options.Shells.Add(new ShellDescriptor
        {
            Name = "admin",
            Shell = ShellId.Admin,
            Side = CallerSide.Admin,
            BasePath = admin.AdminPath,
            ThemeDetector = BlazorAdminTheme,
            LoginPath = admin.LoginPath,
            AuthorizesRoutes = true,
            NotFoundPage = LegacyHostPage,
        });

        // Login is part of the admin shell: it renders the admin document and its page lives
        // in the admin bucket, at the tenant's configured login path.
        options.Shells.Add(new ShellDescriptor
        {
            Name = "login",
            Shell = ShellId.Admin,
            Side = CallerSide.Admin,
            BasePath = admin.LoginPath,
            FixedPage = "/login",
            ThemeDetector = BlazorAdminTheme,
            NotFoundPage = LegacyHostPage,
        });

        // The member shell is a separate, independently-active theme: gating it on the admin
        // theme would make the member portal unreachable on a tenant running another admin
        // theme, so it has no theme detector.
        var memberBase = "/" + memberOptions.Value.MemberUrlPrefix;
        options.Shells.Add(new ShellDescriptor
        {
            Name = "member",
            Shell = ShellId.Member,
            Side = CallerSide.Member,
            BasePath = memberBase,
            LoginPath = memberBase + MemberLoginPage,
            NotFoundPage = MemberNotFoundPage,
            OrganizationInPath = true,
            OrganizationPicker = MemberOrganizationPicker,
        });
    }
}
