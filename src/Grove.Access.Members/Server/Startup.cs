using Grove.Access.Members.Handlers;
using Grove.Access.Members.Indexes;
using Grove.Access.Members.Migrations;
using Grove.Access.Members.Permissions;
using Grove.Access.Members.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Access.Users;
using Grove.Access.Users.Events;
using Grove.Access.Users.Handlers;
using Grove.Access.Users.Services;

namespace Grove.Access.Members;

[Feature("Grove.Access.Members")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<MembersMigrations>();
        services.AddScoped<IUserHierarchyService, UserHierarchyService>();
        services.AddScoped<IMemberService, MemberService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<MemberSessionService>();
        services.AddScoped<MemberOrganizationDirectory>();
        // Member page paths carry the organization (/{member prefix}/{slug}/…), resolved among
        // the signed-in user's own bindings.
        services.AddScoped<Grove.Routing.IShellOrganizationResolver, MemberOrganizationSlugResolver>();
        services.AddScoped<MemberBindingService>();
        services.AddScoped<MemberOrganizationGroups>();
        // The organization owner and the hierarchy bound on managing users (docs/permissions.md).
        services.AddIndexProvider<OrganizationOwnerIndexProvider>();
        services.AddScoped<Grove.Access.IOrganizationOwnership, OrganizationOwnershipService>();
        services.AddScoped<Grove.Access.IUserManagementReach, MemberHierarchyReach>();
        services.AddScoped<Grove.Access.IAccessCeiling, OwnerLockedCeiling>();
        services.AddScoped<Grove.Contents.Management.Handlers.IContentHandler, OrganizationAccessGroupsHandler>();
        services.AddScoped<UserClassService>();
        services.AddScoped<MemberPortalLoginContext>();
        services.AddScoped<MemberImpersonationService>();
        services.AddApiContributor<Api.MembersApi>();
        services.AddApiContributor<Api.MemberPortalApi>();
        // Audit (docs/audit.md): the recorder is a no-op while the audit trail is disabled; the
        // actor handler and the category only matter when it is enabled.
        services.AddScoped<AuditTrail.MemberAuditRecorder>();
        services.AddScoped<Grove.AuditTrail.Services.IAuditTrailEventHandler, AuditTrail.ImpersonationAuditTrailEventHandler>();
        services.AddTransient<Microsoft.Extensions.Options.IConfigureOptions<Grove.AuditTrail.Services.Models.AuditTrailOptions>, AuditTrail.MemberAuditTrailEventConfiguration>();
        services.AddScoped<Grove.Access.ICallerContextContributor, MemberCallerContributor>();
        services.AddScoped<Grove.Access.ICallerRequestResolver, MemberCallerRequestResolver>();
        // Members' access extensions (docs/access.md, Domains onto the extension system): who may
        // manage an organization's members, which binding rows a caller reads, and the
        // organization row scope on content.
        services.AddScoped<Grove.Access.IResourcePermissionMapper, MemberResourcePermissionMapper>();
        services.AddScoped<Grove.Access.IScopeProvider, UserConnectionScopeProvider>();
        services.AddPermissionProvider<MembersPermissionProvider>();

        // Class foundation: the connection index, the creation stamp (every new user is
        // placed in an organization), class claims on the principal, and the login-surface
        // gate (tenant vs portal).
        services.AddIndexProvider<UserConnectionIndexProvider>();
        services.AddIndexProvider<UserHierarchyIndexProvider>();
        services.AddScoped<IUserEventHandler, UserClassStampHandler>();
        services.AddScoped<IUserClaimsProvider, UserClassClaimsProvider>();
        services.AddScoped<ILoginFormEvent, MemberLoginSurfaceGate>();

        // Drives created automatically (docs/media.md § 3): Members knows who is staff and which
        // organizations exist; Media creates the drives when the tenant uses them.
        services.AddScoped<Grove.Media.Files.IMediaDriveOwnerSource, MemberDriveOwnerSource>();
        services.AddScoped<Grove.Contents.Management.Handlers.IContentHandler, OrganizationDriveHandler>();


        // The class permission ceiling (docs/members.md §E). The baseline set is
        // tenant-machinery permissions no member may ever hold; other modules extend it
        // with their own staff-only permissions via Configure<...> next to their
        // IPermissionProvider. Names are the owning modules' permission names verbatim
        // (referenced as strings because the registry must also cover modules this one
        // does not reference, e.g. Grove.Infrastructure.Tenants).
        services.AddScoped<Grove.Access.IAccessCeiling, MemberClassCeiling>();
        services.Configure<MemberPermissionCeilingOptions>(options => options
            .Ceiling(
                "ManageTenants",
                "ManageTenantFeatureProfiles",
                "ManageFeatures",
                "ManageSettings",
                "ManageGeneralSettings",
                "ManageRoles",
                "ViewRoles",
                "ManageUsers",
                "EditUsers",
                "DeleteUsers",
                "ListUsers",
                "View Users",
                "AssignRoleToUsers",
                "DisableTwoFactorAuthenticationForUsers",
                "ManageAdminMenu",
                // Workflow authoring runs as trusted system code once published: staff only.
                "ManageWorkflows",
                Permissions.MembersPermissions.ManageMembers.Name,
                Permissions.MembersPermissions.ImpersonateMembers.Name,
                Permissions.MembersPermissions.ConvertUserClass.Name,
                Permissions.MembersPermissions.TransferOrganizationOwnership.Name,
                // The audit trail is staff-only: members see functional attribution only
                // (docs/audit.md › step 4).
                "ViewAuditTrail",
                "ManageAuditTrailSettings")
            .CeilingPrefix(
                // Dynamic expansions: per-role user management, per-group site settings.
                "ManageUsersInRole_",
                "ListUsersInRole_",
                "AssignRoleToUsers_",
                "ManageResourceSettings"));
    }
}
