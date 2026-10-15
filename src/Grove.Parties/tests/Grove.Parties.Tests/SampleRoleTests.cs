using Grove.Parties.SampleRole;
using Xunit;

namespace Grove.Parties.Tests;

// The sample role is a tenant-kind party type over its own role content type, so the generic
// role pages list it like any host module's role.
public sealed class SampleRoleTests
{
    [Fact]
    public void RegistersOneTenantKindOverItsContentType()
    {
        var type = Assert.Single(new SampleRoleTypeProvider().GetTypes());

        Assert.Equal(SampleRoleConstants.TypeKey, type.Key);
        Assert.Equal(SampleRoleConstants.ContentType, type.ContentType);
        Assert.Equal(Grove.Parties.PartyTypes.PartyTypeKinds.Tenant, type.Kind);
    }
}
