using Crest.AdminTheme.Options;
using Crest.Components.Modules;
using Xunit;

namespace Crest.AdminTheme.Tests;

// The document's shell is read from the configured shell path that follows the tenant base
// the server sent (docs/shells-and-themes.md › The server provides the tenant base).
public sealed class ShellOfTests
{
    [Theory]
    [InlineData("", "/Admin/", "Admin")]
    [InlineData("", "/login/", "Admin")]
    [InlineData("", "/members/", "Member")]
    [InlineData("", "/", "Site")]
    [InlineData("/t2", "/t2/Admin/", "Admin")]
    [InlineData("/t2", "/t2/members/", "Member")]
    [InlineData("/t2", "/t2/", "Site")]
    // A tenant whose prefix happens to end like a shell path is still the site.
    [InlineData("/members", "/members/", "Site")]
    // A member document under an organization: "/members/{slug}/".
    [InlineData("", "/members/acme/", "Member")]
    [InlineData("/t2", "/t2/members/acme/", "Member")]
    public void ShellFollowsTheTenantBase(string tenantBase, string documentBase, string expected)
    {
        var options = new RoutingOptions { TenantBase = tenantBase };

        var shell = ClientWebAssemblyHost.ShellOf(documentBase, options);

        Assert.Equal(expected switch
        {
            "Admin" => CrestShellContext.Admin,
            "Member" => CrestShellContext.Member,
            _ => CrestShellContext.Site,
        }, shell);
    }

    [Theory]
    [InlineData("", "/members/acme/", "acme")]
    [InlineData("/t2", "/t2/members/acme/", "acme")]
    [InlineData("", "/members/", null)]
    [InlineData("", "/Admin/", null)]
    [InlineData("", "/members/acme/files/", null)]
    public void TheOrganizationSlugFollowsTheMemberPath(string tenantBase, string documentBase, string? expected)
    {
        var options = new RoutingOptions { TenantBase = tenantBase };

        Assert.Equal(expected, ClientWebAssemblyHost.OrganizationSlugOf(documentBase, options));
    }
}
