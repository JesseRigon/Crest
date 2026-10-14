using Crest.Members.Handlers;
using Crest.Members.Indexes;
using Crest.Members.Migrations;
using Crest.Members.Permissions;
using Crest.Members.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Users;
using Crest.Users.Events;
using Crest.Users.Handlers;
using Crest.Users.Services;

namespace Crest.Members;

[Feature("Crest.Members")]
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
        services.AddScoped<Crest.Routing.IShellOrganizationResolver, MemberOrganizationSlugResolver>();
        services.AddScoped<MemberBindingService>();
        services.AddScoped<MemberOrganizationGroups>();
        // The organization owner and the hierarchy bound on managing users (docs/permissions.md).
        services.AddIndexProvider<OrganizationOwnerIndexProvider>();
        services.AddScoped<Crest.Access.IOrganizationOwnership, OrganizationOwnershipService>();
        services.AddScoped<Crest.Access.IUserManagementReach, MemberHierarchyReach>();
        services.AddScoped<Crest.Access.IAccessCeiling, OwnerLockedCeiling>();
        services.AddScoped<Crest.ContentManagement.Handlers.IContentHandler, OrganizationAccessGroupsHandler>();
        services.AddScoped<UserClassService>();
        services.AddScoped<MemberPortalLoginContext>();
        services.AddScoped<MemberImpersonationService>();
        // Audit (docs/audit.md): the recorder is a no-op while the audit trail is disabled; the
        // actor handler and the category only matter when it is enabled.
        services.AddScoped<AuditTrail.MemberAuditRecorder>();
        services.AddScoped<Crest.AuditTrail.Services.IAuditTrailEventHandler, AuditTrail.ImpersonationAuditTrailEventHandler>();
        services.AddTransient<Microsoft.Extensions.Options.IConfigureOptions<Crest.AuditTrail.Services.Models.AuditTrailOptions>, AuditTrail.MemberAuditTrailEventConfiguration>();
        services.AddScoped<Crest.Access.ICallerContextContributor, MemberCallerContributor>();
        services.AddScoped<Crest.Access.ICallerRequestResolver, MemberCallerRequestResolver>();
        // Members' access extensions (docs/access.md, Domains onto the extension system): who may
        // manage an organization's members, which binding rows a caller reads, and the
        // organization row scope on content.
        services.AddScoped<Crest.Access.IResourcePermissionMapper, MemberResourcePermissionMapper>();
        services.AddScoped<Crest.Access.IScopeProvider, UserConnectionScopeProvider>();
        services.AddPermissionProvider<MembersPermissionProvider>();

        // Class foundation: the connection index, the creation stamp (every new user is
        // placed in an organization), class claims on the principal, and the login-surface
        // gate (tenant vs portal).
        services.AddIndexProvider<UserConnectionIndexProvider>();
        services.AddIndexProvider<UserHierarchyIndexProvider>();
        services.AddScoped<IUserEventHandler, UserClassStampHandler>();
        services.AddScoped<IUserClaimsProvider, UserClassClaimsProvider>();
        services.AddScoped<ILoginFormEvent, MemberLoginSurfaceGate>();
        services.AddScoped<IUsersAdminListFilterProvider, Users.UserClassAdminListFilterProvider>();

        // Drives created automatically (docs/media.md § 3): Members knows who is staff and which
        // organizations exist; Media creates the drives when the tenant uses them.
        services.AddScoped<Crest.Media.Files.IMediaDriveOwnerSource, MemberDriveOwnerSource>();
        services.AddScoped<Crest.ContentManagement.Handlers.IContentHandler, OrganizationDriveHandler>();


        // The class permission ceiling (docs/members.md §E). The baseline set is
        // tenant-machinery permissions no member may ever hold; other modules extend it
        // with their own staff-only permissions via Configure<...> next to their
        // IPermissionProvider. Names are the owning modules' permission names verbatim
        // (referenced as strings because the registry must also cover modules this one
        // does not reference, e.g. Crest.Tenants).
        services.AddScoped<Crest.Access.IAccessCeiling, MemberClassCeiling>();
        services.Configure<MemberPermissionCeilingOptions>(options => options
            .Ceiling(
                "ManageTenants",
                "ManageTenantFeatureProfiles",
                "ManageFeatures",
                "ManageSettings",
                "ManageGeneralSettings",
                "ManageDebuggingSettings",
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
