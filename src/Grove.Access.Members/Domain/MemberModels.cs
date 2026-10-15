namespace Grove.Access.Members.Models;

/// <summary>
/// One org binding on a member account (ruling: one account, multiple org bindings,
/// different roles per org). <paramref name="Roles"/> holds member ROLE TEMPLATE names
/// (tenant-defined Grove roles); the ACTIVE binding's role claims are contributed to
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

/// <summary>One user's node in one organization's reports-to hierarchy: their direct
/// managers and every user above them by any path.</summary>
public sealed record HierarchyNodeModel(
    string UserId,
    string OrganizationId,
    IReadOnlyList<string> ManagerIds,
    IReadOnlyList<string> AncestorIds);

/// <summary>A user's classes and connections as the class endpoints return them: one
/// connection per (organization, class).</summary>
public sealed record UserConnectionsModel(
    string UserId,
    IReadOnlyList<string> Classes,
    IReadOnlyList<UserConnectionModel> Connections);

public sealed record UserConnectionModel(string OrganizationId, string Class, IReadOnlyList<string> Roles, bool IsMemberAdmin);

/// <summary>Adds a class to a user: staff (the tenant organization) or member (of
/// <paramref name="OrganizationId"/>, which a member class requires).</summary>
public sealed record AddUserClassRequest(string Class, string? OrganizationId, IReadOnlyList<string>? Roles);

/// <summary>The Members tenant settings.</summary>
public sealed record MembersSettingsModel(bool AllowMembersOnTenantOrganization);

/// <summary>Member session view for the current principal: who am I, which orgs, which
/// one is active, and whether this session is an impersonation.</summary>
public sealed record MemberSessionModel(
    MemberModel? Member,
    string? ActiveOrganizationId,
    string? ImpersonatorUserId,
    IReadOnlyList<MemberSessionOrganization> Organizations,
    string? ImpersonatorUserName = null,
    string? SiteName = null);

/// <summary>An organization the session's member is bound to, with its display name and the
/// slug member page paths name it by (<c>/{member prefix}/{slug}/…</c>), unique among this
/// member's organizations.</summary>
public sealed record MemberSessionOrganization(string Id, string Name, string Slug = "");

/// <summary>Portal password recovery: sends a reset link to the member portal.</summary>
public sealed record PortalForgotPasswordRequest(string UserNameOrEmail);

/// <summary>Portal password reset, from the link's code.</summary>
public sealed record PortalResetPasswordRequest(string UserNameOrEmail, string Code, string NewPassword);

/// <summary>Portal sign-in. <paramref name="OrganizationId"/> is optional: when given,
/// the account must be bound to that organization and it becomes the active one;
/// otherwise the member's first binding is activated.</summary>
public sealed record PortalLoginRequest(string UserName, string Password, bool RememberMe, string? OrganizationId);

/// <summary>Portal self-registration: the account, the person it is, and the
/// organization it joins. Registration stamps the member class and the org binding
/// before the user record is first saved. The organization is optional in the request so
/// that its absence is the controller's own refusal, not a model-validation failure.</summary>
public sealed record PortalRegisterRequest(
    string UserName,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? OrganizationId);

/// <summary>What registration did: signed the new member in, or held the account for
/// the tenant's moderation / email confirmation (stock RegistrationSettings).</summary>
public sealed record PortalRegisterResult(bool SignedIn, bool AwaitingModeration, bool AwaitingEmailConfirmation);

/// <summary>The refusal payload of a portal sign-in or registration.</summary>
public sealed record PortalRefusal(IReadOnlyList<string> Errors, string? Redirect = null);

/// <summary>An external sign-in provider the portal can offer.</summary>
public sealed record PortalExternalProvider(string Name, string DisplayName);
