using Crest.Members.Handlers;
using Crest.Members.Indexes;
using Crest.Members.Migrations;
using Crest.Members.Permissions;
using Crest.Members.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Services;

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
        services.AddScoped<MemberStampService>();
        services.AddScoped<MemberPortalLoginContext>();
        services.AddScoped<MemberImpersonationService>();
        services.AddScoped<UserClassConversionService>();
        services.AddTransient<Microsoft.Extensions.Options.IConfigureOptions<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>, MemberCookieEventsConfiguration>();
        services.AddPermissionProvider<MembersPermissionProvider>();

        // Class foundation: index for reliable SQL filtering, creation stamp, class
        // claim on the principal, and the login-surface gate (tenant vs portal).
        services.AddIndexProvider<UserClassIndexProvider>();
        services.AddIndexProvider<MemberOrgBindingIndexProvider>();
        services.AddScoped<IUserEventHandler, UserClassStampHandler>();
        services.AddScoped<IUserClaimsProvider, UserClassClaimsProvider>();
        services.AddScoped<ILoginFormEvent, MemberLoginSurfaceGate>();


        // The class permission ceiling (plans/user-systems.md §E). The baseline set is
        // tenant-machinery permissions no member may ever hold; other modules extend it
        // with their own staff-only permissions via Configure<...> next to their
        // IPermissionProvider. Names are the owning modules' permission names verbatim
        // (referenced as strings because the registry must also cover modules this one
        // does not reference, e.g. OrchardCore.Tenants).
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, MemberPermissionCeilingHandler>();
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
                Permissions.MembersPermissions.ConvertUserClass.Name)
            .CeilingPrefix(
                // Dynamic expansions: per-role user management, per-group site settings.
                "ManageUsersInRole_",
                "ListUsersInRole_",
                "AssignRoleToUsers_",
                "ManageResourceSettings"));
    }
}
