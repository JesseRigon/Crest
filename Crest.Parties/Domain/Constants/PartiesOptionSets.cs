namespace Crest.Parties.Constants;

/// <summary>
/// The logical keys of the Content Part Lists this module owns. Code resolves sets by
/// these keys; the sets themselves are tenant-editable content, so a tenant can
/// relabel or extend them without any of these values changing. The shared
/// vocabularies that cross every domain boundary (honorifics, languages, countries,
/// dial codes) stay Crest GLOBAL lists - these are only the party-domain ones.
/// </summary>
public static class PartiesOptionSets
{
    /// <summary>How a contact point reaches a party (email, phone, ...).</summary>
    public const string ContactPointKind = "parties.contact-point-kind";

    /// <summary>What role an address plays for a party (billing, shipping, ...).</summary>
    public const string AddressKind = "parties.address-kind";

    /// <summary>Legal/structural form of an organization.</summary>
    public const string OrganizationType = "parties.organization-type";

    /// <summary>What sector an organization operates in.</summary>
    public const string Industry = "parties.industry";
}

/// <summary>
/// Seed keys for <see cref="PartiesOptionSets.ContactPointKind"/>. Constants because
/// the contact-point slices branch on them (an email kind renders a mailto, a phone
/// kind dials); tenants extend the list freely - code only ever branches on the keys
/// it knows and treats the rest generically.
/// </summary>
public static class ContactPointKinds
{
    public const string Email = "email";
    public const string Phone = "phone";
    public const string Mobile = "mobile";
    public const string Fax = "fax";
    public const string Website = "website";
    public const string Social = "social";
    public const string Messaging = "messaging";
    public const string Other = "other";
}

/// <summary>
/// Seed keys for <see cref="PartiesOptionSets.AddressKind"/>. Constants because
/// transaction processing will snapshot billing/shipping addresses by kind.
/// </summary>
public static class AddressKinds
{
    public const string Billing = "billing";
    public const string Shipping = "shipping";
    public const string Mailing = "mailing";
    public const string Physical = "physical";
    public const string Other = "other";
}
