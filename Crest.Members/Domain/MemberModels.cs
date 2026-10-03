namespace Crest.Members.Models;

/// <summary>
/// One org binding on a member account (ruling: one account, multiple org bindings,
/// different roles per org). <paramref name="Roles"/> holds member ROLE TEMPLATE names
/// (tenant-defined Orchard roles); the ACTIVE binding's role claims are contributed to
/// the principal per request, so downstream permission checks work unchanged.
/// </summary>
public sealed record MemberOrgBindingModel(
    string OrganizationId,
    IReadOnlyList<string> Roles,
    bool IsMemberAdmin);

/// <summary>A member account as the member APIs expose it.</summary>
public sealed record MemberModel(
    string UserId,
    string UserName,
    string? Email,
    string? PersonId,
    IReadOnlyList<MemberOrgBindingModel> Bindings);

/// <summary>One node of the per-tenant user hierarchy (staff root or an org's root).</summary>
public sealed record HierarchyNodeModel(
    long Id,
    string UserId,
    long? ParentId,
    string RootKind,
    string? OrganizationId,
    string Path,
    int Position);

/// <summary>Root kinds of the shared hierarchy forest.</summary>
public static class HierarchyRootKinds
{
    public const string Staff = "staff";
    public const string Organization = "org";
}

/// <summary>Member session view for the current principal: who am I, which orgs, which
/// one is active, and whether this session is an impersonation.</summary>
public sealed record MemberSessionModel(MemberModel? Member, string? ActiveOrganizationId, string? ImpersonatorUserId);

/// <summary>Portal sign-in. <paramref name="OrganizationId"/> is optional: when given,
/// the account must be bound to that organization and it becomes the active one;
/// otherwise the member's first binding is activated.</summary>
public sealed record PortalLoginRequest(string UserName, string Password, bool RememberMe, string? OrganizationId);

/// <summary>Portal self-registration: the account, the person it is, and the
/// organization it joins. Registration stamps the member class and the org binding
/// before the user record is first saved.</summary>
public sealed record PortalRegisterRequest(
    string UserName,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string OrganizationId);

/// <summary>What registration did: signed the new member in, or held the account for
/// the tenant's moderation / email confirmation (stock RegistrationSettings).</summary>
public sealed record PortalRegisterResult(bool SignedIn, bool AwaitingModeration, bool AwaitingEmailConfirmation);

/// <summary>The refusal payload of a portal sign-in or registration.</summary>
public sealed record PortalRefusal(IReadOnlyList<string> Errors, string? Redirect = null);

/// <summary>An external sign-in provider the portal can offer.</summary>
public sealed record PortalExternalProvider(string Name, string DisplayName);
