namespace Crest.Members.Constants;

public static class MembersConstants
{
    public const string FeatureId = "Crest.Members";
}

/// <summary>
/// The two user classes (design: docs/members.md). The class is a stamped
/// property of the user record, never a role: it decides which login surfaces accept
/// the account at all, so it must not be grantable through the role editor.
/// </summary>
public static class UserClasses
{
    /// <summary>A tenant user: signs in through the tenant login surfaces.</summary>
    public const string Staff = "staff";

    /// <summary>An organization-bound user: signs in only through their org's member
    /// portal; excluded from tenant SSO; subject to the class permission ceiling.</summary>
    public const string Member = "member";
}

/// <summary>Claim and session keys the member system stamps onto principals/sessions.</summary>
public static class MemberClaims
{
    /// <summary>Claim carrying the user's class ("staff"/"member"). Added at principal
    /// creation so the permission ceiling and login gates read the principal, not the
    /// store, on every check.</summary>
    public const string UserClass = "crest_class";

    /// <summary>Claim carrying the ACTIVE organization's content item id for a member
    /// session (contributed per-request from the auth session's active-org item).</summary>
    public const string ActiveOrganization = "crest_active_org";

    /// <summary>Claim present on impersonation sessions: the STAFF user's UserId. Its
    /// presence is what lets the audit layer attribute "staff X impersonating member Y"
    /// while functional attribution stays with the member principal.</summary>
    public const string Impersonator = "crest_impersonator";
}

/// <summary>AuthenticationProperties item keys (server-side session state when a ticket
/// store is active; see docs/members.md §C).</summary>
public static class MemberSessionKeys
{
    public const string ActiveOrganization = "crest-active-org";
    public const string ImpersonatorUserId = "crest-impersonator-user-id";
    public const string ImpersonatorUserName = "crest-impersonator-user-name";

    /// <summary>Set on the EXTERNAL-scheme properties by the portal's external-login
    /// start endpoint: the organization the sign-in/registration is for. The login
    /// surface gate and the creation stamp read it back from the external cookie on
    /// the callback, so an account provisioned through a portal external login is a
    /// member of that org - never a default staff account.</summary>
    public const string PortalOrganization = "crest-portal-org";
}

/// <summary>
/// The member ROLE TEMPLATES: ordinary Crest roles the tenant shapes in the normal
/// role editor (tenant governs powers); an org binding names which template the member
/// holds in that org. Never one Crest role per organization - the roles document is a
/// single cached per-tenant blob and must not scale with org count.
/// </summary>
public static class MemberRoleTemplates
{
    public const string Member = "Member";
    public const string MemberAdministrator = "Member Administrator";
}
