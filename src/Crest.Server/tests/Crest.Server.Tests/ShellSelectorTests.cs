using Crest.Access;
using Crest.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Crest.Server.Tests;

// The shell registry (docs/shells-and-themes.md › A registry of shells): the selector takes the
// request and matches registered bases by path prefix, longest first; the site is the fallback.
public sealed class ShellSelectorTests
{
    private static PathPrefixShellSelector Selector(params ShellDescriptor[] shells)
    {
        var options = new ShellRegistryOptions();
        options.Shells.AddRange(shells);
        return new PathPrefixShellSelector(Options.Create(options));
    }

    private static ShellDescriptor Shell(string name, string basePath, ShellId shell) => new()
    {
        Name = name,
        Shell = shell,
        Side = CallerSide.Admin,
        BasePath = basePath,
        NotFoundPage = "/not-found",
    };

    private static HttpRequest Request(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context.Request;
    }

    [Theory]
    [InlineData("/Admin/Features", "admin", "/Features")]
    [InlineData("/admin", "admin", "")]
    [InlineData("/members/acme/files", "member", "/acme/files")]
    [InlineData("/members-area", null, null)]
    [InlineData("/", null, null)]
    public void SelectsByPathPrefix(string path, string? expected, string? remainder)
    {
        var selector = Selector(
            Shell("admin", "/Admin", ShellId.Admin),
            Shell("member", "/members", ShellId.Member));

        var selection = selector.Select(Request(path));

        Assert.Equal(expected, selection?.Descriptor.Name);
        if (selection is not null)
        {
            Assert.Equal(remainder, selection.Remainder.Value ?? string.Empty);
        }
    }

    [Fact]
    public void TheLongestBaseWins()
    {
        var selector = Selector(
            Shell("short", "/portal", ShellId.Member),
            Shell("long", "/portal/staff", ShellId.Admin));

        Assert.Equal("long", selector.Select(Request("/portal/staff/x"))?.Descriptor.Name);
        Assert.Equal("short", selector.Select(Request("/portal/x"))?.Descriptor.Name);
    }

    [Fact]
    public void ShellIdsComeFromTheAssemblyDeclaration()
    {
        Assert.Equal(ShellId.Member, new ShellId(Crest.Components.Modules.CrestShells.Member));
        Assert.Equal("admin", ShellId.Admin.ToString());
    }

    [Theory]
    [InlineData("Acme.Orders.BlazorWasm", "Acme.Orders")]
    [InlineData("Crest.Members.Member.BlazorWasm", "Crest.Members")]
    [InlineData("Other.BlazorWasm", null)]
    public void ModuleClientLibrariesBelongToTheLongestModuleId(string assembly, string? expected)
    {
        Assert.Equal(expected, ModuleClientAssemblies.OwningExtensionId(assembly, ["Crest", "Crest.Members", "Acme.Orders"]));
    }
}

// An organization-free member page (login, the picker) acts in no organization: it is
// admitted on the site side, so a staff-only user or a member with no bindings sees it rather
// than the member caller's "must name an organization" refusal (live finding 2026-10-13).
public sealed class ShellPageSideTests
{
    private static ShellDescriptor Member(bool organizationInPath) => new()
    {
        Name = "member",
        Shell = ShellId.Member,
        Side = CallerSide.Member,
        BasePath = "/app",
        NotFoundPage = "/not-found",
        OrganizationInPath = organizationInPath,
    };

    private static RouteComponentEntry Page(bool organizationFree) =>
        new("/login", typeof(object), ShellId.Member, AllowsAnonymous: true, IsOrganizationFree: organizationFree);

    [Fact]
    public void AnOrganizationFreePageTakesTheSiteSide() =>
        Assert.Equal(CallerSide.Site, Crest.Middlewares.ShellSelectionMiddleware.SideFor(Member(true), Page(true)));

    [Fact]
    public void AnOrganizationPageKeepsTheMemberSide() =>
        Assert.Equal(CallerSide.Member, Crest.Middlewares.ShellSelectionMiddleware.SideFor(Member(true), Page(false)));

    [Fact]
    public void AMissAndAShellWithoutOrganizationsKeepTheirSide()
    {
        Assert.Equal(CallerSide.Member, Crest.Middlewares.ShellSelectionMiddleware.SideFor(Member(true), null));
        Assert.Equal(CallerSide.Member, Crest.Middlewares.ShellSelectionMiddleware.SideFor(Member(false), Page(true)));
    }
}
