using Crest.Data.Global.Lists;
using Crest.ContentGroups;
using Crest.Parties.Constants;

namespace Crest.Parties.Navigation;

/// <summary>Declares the "Parties" content group: the party types plus every option
/// list that only exists to describe a party (the global name lists included - they
/// have no owner of their own and are only ever attached to Person).</summary>
public sealed class PartiesContentGroupProvider : IContentGroupProvider
{
    public const string Key = "parties";

    public void Build(ContentGroupBuilder builder) => builder
        .Group(Key, "Parties", 10)
        .Types(PartiesConstants.ContentTypes.Person, PartiesConstants.ContentTypes.Organization, PartiesConstants.ContentTypes.Association)
        .Lists(
            PartiesOptionSets.ContactPointKind,
            PartiesOptionSets.AddressKind,
            PartiesOptionSets.OrganizationType,
            PartiesOptionSets.OrganizationNameKind,
            PartiesOptionSets.AssociationType,
            GlobalLists.Naics,
            GlobalLists.Honorifics,
            GlobalLists.NameSuffixes);
}
