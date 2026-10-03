using Crest.Parties.Constants;
using Crest.Parties.ViewModels;
using Xunit;

namespace Crest.Parties.Tests;

// The "which one is THE email / THE phone / THE billing address" rules. Pure Domain
// logic, so the server mapper and any Blazor page agree by construction.
public class PartyContactRulesTests
{
    private static ContactPointModel Point(string id, string? kind, string value, bool preferred = false) =>
        new(id, kind, value, null, preferred, null);

    private static AddressModel Address(string id, string? kind, string? city, bool preferred = false) =>
        new(id, kind, null, null, city, null, null, new Dictionary<int, string>(), [], null, null, preferred);

    [Fact]
    public void Primary_PreferredOfKind_Wins()
    {
        var points = new[]
        {
            Point("a", ContactPointKinds.Email, "first@example.com"),
            Point("b", ContactPointKinds.Email, "preferred@example.com", preferred: true),
        };

        Assert.Equal("b", PartyContactRules.Primary(points, ContactPointKinds.Email)?.Id);
    }

    [Fact]
    public void Primary_NoPreferred_FirstOfKindInStoredOrder()
    {
        var points = new[]
        {
            Point("a", ContactPointKinds.Phone, "111"),
            Point("b", ContactPointKinds.Phone, "222"),
        };

        Assert.Equal("a", PartyContactRules.Primary(points, ContactPointKinds.Phone)?.Id);
    }

    [Fact]
    public void Primary_KindFamily_EarlierKindOutranksLaterUnlessPreferred()
    {
        // A landline listed AFTER a mobile still wins for [phone, mobile]...
        var points = new[]
        {
            Point("mobile", ContactPointKinds.Mobile, "222"),
            Point("landline", ContactPointKinds.Phone, "111"),
        };
        Assert.Equal("landline", PartyContactRules.Primary(points, ContactPointKinds.Phone, ContactPointKinds.Mobile)?.Id);

        // ...unless the mobile is the preferred one.
        var preferredMobile = new[]
        {
            Point("mobile", ContactPointKinds.Mobile, "222", preferred: true),
            Point("landline", ContactPointKinds.Phone, "111"),
        };
        Assert.Equal("mobile", PartyContactRules.Primary(preferredMobile, ContactPointKinds.Phone, ContactPointKinds.Mobile)?.Id);
    }

    [Fact]
    public void Primary_IgnoresOtherKindsAndUnresolvedKinds()
    {
        var points = new[]
        {
            Point("site", ContactPointKinds.Website, "https://example.com", preferred: true),
            Point("unknown", null, "???", preferred: true),
        };

        Assert.Null(PartyContactRules.Primary(points, ContactPointKinds.Email));
    }

    [Fact]
    public void Primary_KindComparisonIsCaseInsensitive()
    {
        var points = new[] { Point("a", "Email", "x@example.com") };

        Assert.Equal("a", PartyContactRules.Primary(points, ContactPointKinds.Email)?.Id);
    }

    [Fact]
    public void PrimaryAddress_PreferredThenFirstOfKind()
    {
        var addresses = new[]
        {
            Address("ship1", AddressKinds.Shipping, "Portland"),
            Address("bill1", AddressKinds.Billing, "Denver"),
            Address("bill2", AddressKinds.Billing, "Boise", preferred: true),
        };

        Assert.Equal("bill2", PartyContactRules.Primary(addresses, AddressKinds.Billing)?.Id);
        Assert.Equal("ship1", PartyContactRules.Primary(addresses, AddressKinds.Shipping)?.Id);
        Assert.Null(PartyContactRules.Primary(addresses, AddressKinds.Mailing));
    }

    [Fact]
    public void FormatSingleLine_SkipsBlankParts_NullWhenEmpty()
    {
        var address = new AddressModel("a", AddressKinds.Billing, " 1 Main St ", null, "Boise", "83702", "US", new Dictionary<int, string> { [1] = "US", [2] = "US-ID" }, [], null, null, false);

        Assert.Equal("1 Main St, Boise, 83702, US", PartyContactRules.FormatSingleLine(address));
        Assert.Null(PartyContactRules.FormatSingleLine(Address("b", AddressKinds.Billing, city: "  ")));
    }
}
