using Grove.Access.Members.Services;
using Xunit;

namespace Grove.Access.Members.Tests;

// Member page paths carry the organization (docs/shells-and-themes.md › Member sessions and
// sides): a slug derived from the organization's name.
public sealed class OrganizationSlugTests
{
    [Theory]
    [InlineData("Acme Corp", "acme-corp")]
    [InlineData("  Café  Société ", "cafe-societe")]
    [InlineData("A/B & C", "a-b-c")]
    [InlineData("---", "")]
    public void SlugsAreLowerCaseAsciiWords(string name, string expected) =>
        Assert.Equal(expected, MemberOrganizationDirectory.Slugify(name));
}
