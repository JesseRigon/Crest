#nullable enable

using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.Environment.Shell;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Grove.AuditTrail.Recorders;

/// <summary>
/// The tenant-machinery writes that never go through the content manager (docs/audit.md ›
/// step 3): roles, settings, features and themes. Users are recorded by Grove.Users' audit
/// feature, members and bindings by Grove.Members, workflow runs by Grove.Workflows.
/// </summary>
public sealed class TenantAuditTrailEventConfiguration : IConfigureOptions<AuditTrailOptions>
{
    public const string Roles = nameof(Roles);
    public const string RoleCreated = "Created";
    public const string RoleUpdated = "Updated";
    public const string RoleRemoved = "Removed";

    public const string Settings = nameof(Settings);
    public const string SettingsUpdated = "Updated";

    public const string Features = nameof(Features);
    public const string FeatureEnabled = "Enabled";
    public const string FeatureDisabled = "Disabled";

    public const string Themes = nameof(Themes);
    public const string SiteThemeChanged = "SiteThemeChanged";
    public const string AdminThemeChanged = "AdminThemeChanged";

    public void Configure(AuditTrailOptions options)
    {
        options.For<TenantAuditTrailEventConfiguration>(Roles, S => S["Roles"])
            .WithEvent(RoleCreated, S => S["Created"], S => S["A role was created."], true)
            .WithEvent(RoleUpdated, S => S["Updated"], S => S["A role's permissions were changed."], true)
            .WithEvent(RoleRemoved, S => S["Removed"], S => S["A role was deleted."], true);

        options.For<TenantAuditTrailEventConfiguration>(Settings, S => S["Settings"])
            .WithEvent(SettingsUpdated, S => S["Updated"], S => S["Site settings were changed."], true);

        options.For<TenantAuditTrailEventConfiguration>(Features, S => S["Features"])
            .WithEvent(FeatureEnabled, S => S["Enabled"], S => S["A feature was enabled."], true)
            .WithEvent(FeatureDisabled, S => S["Disabled"], S => S["A feature was disabled."], true);

        options.For<TenantAuditTrailEventConfiguration>(Themes, S => S["Themes"])
            .WithEvent(SiteThemeChanged, S => S["Site theme changed"], S => S["The site theme was changed."], true)
            .WithEvent(AdminThemeChanged, S => S["Admin theme changed"], S => S["The admin theme was changed."], true);
    }
}

/// <summary>The data of a tenant-machinery event: what changed, never the values of settings.</summary>
public sealed class TenantAuditTrailEvent
{
    /// <summary>The role, the feature, or the theme.</summary>
    public string? Name { get; set; }

    /// <summary>The settings sections that changed.</summary>
    public List<string> Sections { get; set; } = [];

    public string? Previous { get; set; }

    public string? Current { get; set; }
}

/// <summary>Writes a tenant-machinery event as the request's user (the actor handlers complete it).</summary>
public sealed class TenantAuditTrailRecorder(IAuditTrailManager manager, IHttpContextAccessor httpContextAccessor)
{
    public Task RecordAsync(string category, string name, string correlationId, TenantAuditTrailEvent data, string recordType, string? displayText)
    {
        var user = httpContextAccessor.HttpContext?.User;
        return manager.RecordEventAsync(new AuditTrailContext<TenantAuditTrailEvent>(
            name,
            category,
            correlationId,
            user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            user?.Identity?.Name ?? string.Empty,
            data)
        {
            Subject = new AuditTrailSubject { RecordId = correlationId, RecordType = recordType, DisplayText = displayText },
        });
    }
}

public sealed class RoleAuditTrailEventHandler(TenantAuditTrailRecorder recorder) :
    Grove.Security.IRoleCreatedEventHandler,
    Grove.Security.IRoleUpdatedEventHandler,
    Grove.Security.IRoleRemovedEventHandler
{
    public Task RoleCreatedAsync(string roleName) => RecordAsync(TenantAuditTrailEventConfiguration.RoleCreated, roleName);

    public Task RoleUpdatedAsync(string roleName) => RecordAsync(TenantAuditTrailEventConfiguration.RoleUpdated, roleName);

    public Task RoleRemovedAsync(string roleName) => RecordAsync(TenantAuditTrailEventConfiguration.RoleRemoved, roleName);

    private Task RecordAsync(string name, string roleName) => recorder.RecordAsync(
        TenantAuditTrailEventConfiguration.Roles, name, roleName, new TenantAuditTrailEvent { Name = roleName }, "Role", roleName);
}

public sealed class FeatureAuditTrailEventHandler(IServiceProvider services) : Grove.Environment.Shell.FeatureEventHandler
{
    public override Task EnabledAsync(Grove.Environment.Extensions.Features.IFeatureInfo feature)
        => RecordAsync(TenantAuditTrailEventConfiguration.FeatureEnabled, feature);

    public override Task DisabledAsync(Grove.Environment.Extensions.Features.IFeatureInfo feature)
        => RecordAsync(TenantAuditTrailEventConfiguration.FeatureDisabled, feature);

    private Task RecordAsync(string name, Grove.Environment.Extensions.Features.IFeatureInfo feature)
    {
        // The audit trail's own enabling runs before its tables exist.
        if (feature.Id == "Grove.AuditTrail")
        {
            return Task.CompletedTask;
        }

        return services.GetRequiredService<TenantAuditTrailRecorder>().RecordAsync(
            TenantAuditTrailEventConfiguration.Features, name, feature.Id, new TenantAuditTrailEvent { Name = feature.Id }, "Feature", feature.Name);
    }
}
