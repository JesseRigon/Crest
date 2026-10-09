using Crest.Parties.ViewModels;
using Crest.ContentManagement;

namespace Crest.Parties.Services;

/// <summary>
/// Reads and writes a party's contained contact data (the ContactPoints and Addresses
/// bags on Person/Organization). Callers hand in the party item they have already
/// loaded AND authorized - this service never authorizes; it is the write path the
/// controller and other modules share. Writes persist the party in place.
/// </summary>
public interface IPartyContactsService
{
    Task<PartyContactsModel> ReadAsync(ContentItem party, CancellationToken cancellationToken = default);

    Task<ContactPointModel> AddContactPointAsync(ContentItem party, ContactPointWriteModel write, CancellationToken cancellationToken = default);

    /// <summary>Null when no contact point with that id is on the party.</summary>
    Task<ContactPointModel?> UpdateContactPointAsync(ContentItem party, string contactPointId, ContactPointWriteModel write, CancellationToken cancellationToken = default);

    Task<bool> RemoveContactPointAsync(ContentItem party, string contactPointId, CancellationToken cancellationToken = default);

    Task<AddressModel> AddAddressAsync(ContentItem party, AddressWriteModel write, CancellationToken cancellationToken = default);

    Task<AddressModel?> UpdateAddressAsync(ContentItem party, string addressId, AddressWriteModel write, CancellationToken cancellationToken = default);

    Task<bool> RemoveAddressAsync(ContentItem party, string addressId, CancellationToken cancellationToken = default);
}
