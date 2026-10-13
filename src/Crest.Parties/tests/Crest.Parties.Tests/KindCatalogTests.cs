using Crest.Kinds;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;
using Xunit;

namespace Crest.Parties.Tests;

// The generic kind registry (Crest.Kinds) as Parties uses it: merged by key, first
// registration wins, ordered by position then name, found ignoring case.
public sealed class KindCatalogTests
{
    private sealed class Types(params PartyTypeDescriptor[] types) : IPartyTypeProvider
    {
        public IEnumerable<PartyTypeDescriptor> GetTypes() => types;
    }

    [Fact]
    public void TheFirstRegistrationOfAKeyWinsAndTheListIsOrdered()
    {
        var catalog = new PartyTypeCatalog(
        [
            new BasePartyTypeProvider(),
            new Types(
                new() { Key = "CONTACTS", DisplayName = "Shadow", Position = 0 },
                new() { Key = "vendors", DisplayName = "Vendors", ContentType = "Vendor", Position = 30 },
                new() { Key = "customers", DisplayName = "Customers", ContentType = "Customer", Position = 30 }),
        ]);

        Assert.Equal(["contacts", "organizations", "customers", "vendors"], catalog.Kinds.Select(kind => kind.Key));
        Assert.Equal("Contacts", catalog.Find("Contacts")!.DisplayName);
        Assert.Equal("vendors", catalog.ForContentType("Vendor")!.Key);
        Assert.Null(catalog.Find("nobody"));
    }

    [Fact]
    public void AKindWithoutItsOwnRouteIsServedByTheRegistrysPage()
    {
        var own = new PartyTypeDescriptor { Key = "authorities", DisplayName = "Authorities", Route = "/Taxes/Authorities" };
        var generic = new PartyTypeDescriptor { Key = "customers", DisplayName = "Customers" };

        Assert.Equal("/Taxes/Authorities", PartyTypeCatalog.RouteOf(own));
        Assert.Equal("/Parties/customers", PartyTypeCatalog.RouteOf(generic));
        Assert.Equal("/x/customers", KindCatalog<PartyTypeDescriptor>.RouteOf(generic, key => $"/x/{key}"));
    }
}
