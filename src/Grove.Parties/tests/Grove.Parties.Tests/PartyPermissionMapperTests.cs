using Grove.Access;
using Grove.ContentManagement;
using Grove.Parties.Services;
using Xunit;

namespace Grove.Parties.Tests;

// The parties rules (docs/access.md § Members, ruling 2026-10-13): an organization's contacts
// are decided by ManageOrgMembers re-asked against that organization; the content candidates
// stay; other items and permissions are not claimed.
public class PartyPermissionMapperTests
{
    private static readonly CallerContext Member = new() { Tenant = "t", Side = CallerSide.Member, UserId = "u-1", UserClass = "member", OrganizationId = "org-a" };

    [Fact]
    public async Task An_organization_is_its_own_contacts_and_keeps_the_content_candidates()
    {
        var organization = new ContentItem { ContentItemId = "org-a", ContentType = "Organization" };
        var candidates = await new PartyPermissionMapper(null!).MapAsync("EditContent", organization, Member, TestContext.Current.CancellationToken);

        Assert.Contains(candidates!, candidate => candidate.Permission == OrganizationPermissionNames.ManageOrgMembers && candidate.Resource is OrganizationResource { OrganizationId: "org-a" });
        Assert.Contains(candidates!, candidate => candidate.Permission != OrganizationPermissionNames.ManageOrgMembers);
    }

    [Fact]
    public async Task Other_items_and_permissions_are_not_claimed()
    {
        var mapper = new PartyPermissionMapper(null!);
        Assert.Null(await mapper.MapAsync("EditContent", new ContentItem { ContentItemId = "x", ContentType = "Article" }, Member, TestContext.Current.CancellationToken));
        Assert.Null(await mapper.MapAsync("DeleteContent", new ContentItem { ContentItemId = "org-a", ContentType = "Organization" }, Member, TestContext.Current.CancellationToken));
    }
}
