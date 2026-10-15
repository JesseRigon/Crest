using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;
using Grove.Parties.Constants;
using Grove.Parties.Definitions;
using Grove.Parties.Services;
using Xunit;

namespace Grove.Parties.Tests;

public class PartyRoleBaseTests
{
    [Fact]
    public void EachBase_PointsAtItsOwnPartyType()
    {
        Assert.Equal(PartiesConstants.ContentTypes.Organization, PartyRoleBase.Organization.ContentType());
        Assert.Equal(PartiesConstants.ContentTypes.Person, PartyRoleBase.Person.ContentType());
    }

    [Fact]
    public void IndividualOrganization_IsAnOrganizationFlaggedIndividual()
    {
        var organization = new ContentItem { ContentType = PartiesConstants.ContentTypes.Organization };
        Assert.False(IndividualOrganizationService.IsIndividual(organization));

        organization.Alter<ContentPart>(PartiesConstants.ContentTypes.Organization, part =>
            part.Alter<BooleanField>(PartiesConstants.OrganizationFields.Individual, field => field.Value = true));
        Assert.True(IndividualOrganizationService.IsIndividual(organization));

        var person = new ContentItem { ContentType = PartiesConstants.ContentTypes.Person };
        Assert.False(IndividualOrganizationService.IsIndividual(person));
    }
}
