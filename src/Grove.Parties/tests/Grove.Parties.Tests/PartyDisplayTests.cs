using System.Text.Json.Nodes;
using Grove.Parties.Constants;
using Grove.Parties.PartyTypes;
using Grove.Parties.ViewModels;
using Xunit;

namespace Grove.Parties.Tests;

public class PartyDisplayTests
{
    private static JsonObject IndividualContent(bool individual) => new()
    {
        [PartiesConstants.ContentTypes.Organization] = new JsonObject
        {
            [PartiesConstants.OrganizationFields.Individual] = new JsonObject { ["Value"] = individual },
        },
    };

    [Fact]
    public void IndividualOrganization_ReadsAsIndividual()
    {
        Assert.True(PartyDisplay.IsIndividual(PartiesConstants.ContentTypes.Organization, IndividualContent(true)));
        Assert.Equal("Individual", PartyDisplay.KindLabel(PartiesConstants.ContentTypes.Organization, IndividualContent(true)));
    }

    [Fact]
    public void OrdinaryOrganizationAndPerson_AreNotIndividual()
    {
        Assert.False(PartyDisplay.IsIndividual(PartiesConstants.ContentTypes.Organization, IndividualContent(false)));
        Assert.False(PartyDisplay.IsIndividual(PartiesConstants.ContentTypes.Organization, null));
        Assert.False(PartyDisplay.IsIndividual(PartiesConstants.ContentTypes.Person, IndividualContent(true)));
        Assert.Equal(PartiesConstants.ContentTypes.Organization, PartyDisplay.KindLabel(PartiesConstants.ContentTypes.Organization, null));
    }

    [Fact]
    public void PortalUserId_ReadsThePersonsPicker()
    {
        var linked = new JsonObject { [PartiesConstants.ContentTypes.Person] = new JsonObject { ["PortalUser"] = new JsonObject { ["UserIds"] = new JsonArray("u1") } } };
        Assert.Equal("u1", PartyDisplay.PortalUserId(linked));
        Assert.Null(PartyDisplay.PortalUserId(new JsonObject()));
    }

    [Fact]
    public void RoleTypes_AreTenantKindsOtherThanTheBaseParties()
    {
        PartyTypeModel Type(string key, string kind, string? contentType) => new(key, key, kind, contentType, "/", null, 0, null, false);
        var roles = PartyDisplay.RoleTypes(
        [
            Type(PartiesConstants.PartyTypeKeys.Contacts, PartyTypeKinds.Tenant, PartiesConstants.ContentTypes.Person),
            Type(PartiesConstants.PartyTypeKeys.Organizations, PartyTypeKinds.Tenant, PartiesConstants.ContentTypes.Organization),
            Type("customers", PartyTypeKinds.Tenant, "Customer"),
            Type("tax-authorities", PartyTypeKinds.Global, null),
        ]);

        Assert.Equal("customers", Assert.Single(roles).Key);
    }
}
