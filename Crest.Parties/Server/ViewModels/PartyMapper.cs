using Crest.Parties.Constants;
using Crest.Parties.Services;
using Crest.ContentManagement;

namespace Crest.Parties.ViewModels;

// Content-item -> PartyModel projection. A scoped service rather than a static helper
// because Email/Phone are derived from the contact-points bag, whose kinds resolve
// through the tenant's option lists.
public sealed class PartyMapper(IPartyContactsService contacts)
{
    public async Task<PartyModel> ToModelAsync(ContentItem source, CancellationToken cancellationToken = default)
    {
        var contactData = await contacts.ReadAsync(source, cancellationToken);

        return new PartyModel(
            source.ContentItemId,
            source.ContentType,
            source.DisplayText,
            PartyContactRules.Primary(contactData.ContactPoints, ContactPointKinds.Email)?.Value,
            PartyContactRules.Primary(contactData.ContactPoints, ContactPointKinds.Phone, ContactPointKinds.Mobile)?.Value);
    }
}
