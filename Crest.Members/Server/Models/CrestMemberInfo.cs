namespace Crest.Members.Models;

/// <summary>One org binding as stored on the member's user record: which organization,
/// which member ROLE TEMPLATES apply there (tenant-defined Crest roles - the tenant
/// governs powers, the binding only points), and whether this member is the org's
/// member admin.</summary>
public class MemberOrgBinding
{
    public string OrganizationId { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];

    public bool IsMemberAdmin { get; set; }
}

/// <summary>
/// The member aspect stored in <c>User.Properties</c> (same mechanism as
/// <see cref="CrestUserClass"/>). One account, many bindings (ruling 2026-09-09);
/// the ACTIVE binding is session state, never stored here.
/// </summary>
public class CrestMemberInfo
{
    /// <summary>The linked Person content item id (the party model carries the
    /// person↔organization relationship; this record carries authentication).</summary>
    public string? PersonId { get; set; }

    public List<MemberOrgBinding> Bindings { get; set; } = [];
}
