using Crest.Abstractions.Setup;
using Crest.Email;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;
using Crest.Modules;
using Crest.Setup.Services;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crest.Tenants.Workflows;

/// <summary>What the tenant activities share: the tenant name and the default-shell rule.</summary>
public abstract class TenantActivityBase : Activity
{
    [Input(DisplayName = "Tenant name", UIHint = InputUIHints.SingleLine)]
    public Input<string> TenantName { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    /// <summary>The tenant name, or null after failing: tenants are managed from the default shell only.</summary>
    protected async ValueTask<string?> ResolveTenantNameAsync(ActivityExecutionContext context)
    {
        if (!ShellScope.Context.Settings.IsDefaultShell())
        {
            await FailAsync(context, "Tenants are managed from the default tenant only.");
            return null;
        }

        var tenantName = TenantName.GetOrDefault(context)?.Trim();
        if (string.IsNullOrEmpty(tenantName))
        {
            await FailAsync(context, "No tenant name.");
            return null;
        }

        return tenantName;
    }

    protected async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.GetRequiredService<ILoggerFactory>().CreateLogger(GetType()).LogWarning("{Activity} failed: {Reason}", GetType().Name, reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>Creates a tenant's settings, uninitialized, for a later setup.</summary>
[Activity("Crest.Tenants", "Tenant", "Creates a tenant (its shell settings, uninitialized) from the default tenant.", DisplayName = "Create tenant")]
[FlowNode("Done", "Failed")]
public class CreateTenant : TenantActivityBase
{
    [Input(DisplayName = "Description", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Description { get; set; } = null!;

    [Input(DisplayName = "Request URL prefix", UIHint = InputUIHints.SingleLine)]
    public Input<string?> RequestUrlPrefix { get; set; } = null!;

    [Input(DisplayName = "Request URL host", UIHint = InputUIHints.SingleLine)]
    public Input<string?> RequestUrlHost { get; set; } = null!;

    [Input(DisplayName = "Database provider", UIHint = InputUIHints.SingleLine)]
    public Input<string?> DatabaseProvider { get; set; } = null!;

    [Input(DisplayName = "Connection string", UIHint = InputUIHints.SingleLine)]
    public Input<string?> ConnectionString { get; set; } = null!;

    [Input(DisplayName = "Table prefix", UIHint = InputUIHints.SingleLine)]
    public Input<string?> TablePrefix { get; set; } = null!;

    [Input(DisplayName = "Schema", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Schema { get; set; } = null!;

    [Input(DisplayName = "Recipe name", UIHint = InputUIHints.SingleLine)]
    public Input<string?> RecipeName { get; set; } = null!;

    [Input(DisplayName = "Feature profile", UIHint = InputUIHints.SingleLine)]
    public Input<string?> FeatureProfile { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var tenantName = await ResolveTenantNameAsync(context);
        if (tenantName is null)
        {
            return;
        }

        var shellHost = context.GetRequiredService<IShellHost>();
        if (shellHost.TryGetSettings(tenantName, out _))
        {
            await FailAsync(context, $"A tenant named '{tenantName}' exists.");
            return;
        }

        using var shellSettings = context.GetRequiredService<IShellSettingsManager>()
            .CreateDefaultSettings()
            .AsUninitialized()
            .AsDisposable();

        shellSettings.Name = tenantName;
        Set(shellSettings, "Description", Description.GetOrDefault(context));
        var host = RequestUrlHost.GetOrDefault(context)?.Trim();
        if (!string.IsNullOrEmpty(host))
        {
            shellSettings.RequestUrlHost = host;
        }

        var prefix = RequestUrlPrefix.GetOrDefault(context)?.Trim();
        if (!string.IsNullOrEmpty(prefix))
        {
            shellSettings.RequestUrlPrefix = prefix;
        }

        shellSettings.AsUninitialized();
        Set(shellSettings, "ConnectionString", ConnectionString.GetOrDefault(context));
        Set(shellSettings, "TablePrefix", TablePrefix.GetOrDefault(context));
        Set(shellSettings, "Schema", Schema.GetOrDefault(context));
        Set(shellSettings, "DatabaseProvider", DatabaseProvider.GetOrDefault(context));
        Set(shellSettings, "RecipeName", RecipeName.GetOrDefault(context));
        Set(shellSettings, "FeatureProfile", FeatureProfile.GetOrDefault(context));
        shellSettings["Secret"] = Guid.NewGuid().ToString();

        await shellHost.UpdateShellSettingsAsync(shellSettings);
        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    private static void Set(ShellSettings settings, string key, string? value)
    {
        value = value?.Trim();
        if (!string.IsNullOrEmpty(value))
        {
            settings[key] = value;
        }
    }
}

/// <summary>Runs setup on an uninitialized tenant.</summary>
[Activity("Crest.Tenants", "Tenant", "Sets up an uninitialized tenant: site name, administrator, database and recipe.", DisplayName = "Setup tenant")]
[FlowNode("Done", "Failed")]
public class SetupTenant : TenantActivityBase
{
    [Input(DisplayName = "Site name", UIHint = InputUIHints.SingleLine)]
    public Input<string?> SiteName { get; set; } = null!;

    [Input(DisplayName = "Admin user name", UIHint = InputUIHints.SingleLine)]
    public Input<string> AdminUserName { get; set; } = null!;

    [Input(DisplayName = "Admin email", UIHint = InputUIHints.SingleLine)]
    public Input<string> AdminEmail { get; set; } = null!;

    [Input(DisplayName = "Admin password", UIHint = InputUIHints.SingleLine)]
    public Input<string?> AdminPassword { get; set; } = null!;

    [Input(DisplayName = "Database provider", Description = "Empty = the tenant's settings.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> DatabaseProvider { get; set; } = null!;

    [Input(DisplayName = "Connection string", Description = "Empty = the tenant's settings.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> DatabaseConnectionString { get; set; } = null!;

    [Input(DisplayName = "Table prefix", Description = "Empty = the tenant's settings.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> DatabaseTablePrefix { get; set; } = null!;

    [Input(DisplayName = "Schema", Description = "Empty = the tenant's settings.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> DatabaseSchema { get; set; } = null!;

    [Input(DisplayName = "Recipe name", Description = "Empty = the tenant's settings.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> RecipeName { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var tenantName = await ResolveTenantNameAsync(context);
        if (tenantName is null)
        {
            return;
        }

        var shellHost = context.GetRequiredService<IShellHost>();
        if (!shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            await FailAsync(context, $"No tenant '{tenantName}'.");
            return;
        }

        if (shellSettings.IsRunning() || !shellSettings.IsUninitialized())
        {
            await FailAsync(context, $"Tenant '{tenantName}' is already set up.");
            return;
        }

        var adminUserName = AdminUserName.GetOrDefault(context)?.Trim();
        var allowed = context.GetRequiredService<IOptions<IdentityOptions>>().Value.User.AllowedUserNameCharacters;
        if (string.IsNullOrEmpty(adminUserName) || adminUserName.Any(c => !allowed.Contains(c)))
        {
            await FailAsync(context, "The admin user name is missing or holds characters the identity options refuse.");
            return;
        }

        var adminEmail = AdminEmail.GetOrDefault(context)?.Trim();
        if (string.IsNullOrEmpty(adminEmail) || !context.GetRequiredService<IEmailAddressValidator>().Validate(adminEmail))
        {
            await FailAsync(context, "The admin e-mail is missing or invalid.");
            return;
        }

        var setupService = context.GetRequiredService<ISetupService>();
        var recipeName = Or(RecipeName.GetOrDefault(context), shellSettings["RecipeName"]);
        var recipes = await setupService.GetSetupRecipesAsync();
        var setupContext = new SetupContext
        {
            ShellSettings = shellSettings,
            EnabledFeatures = null,
            Errors = new Dictionary<string, string>(),
            Recipe = recipes.FirstOrDefault(r => r.Name == recipeName),
            Properties = new Dictionary<string, object>
            {
                { SetupConstants.SiteName, SiteName.GetOrDefault(context)?.Trim() ?? tenantName },
                { SetupConstants.AdminUsername, adminUserName },
                { SetupConstants.AdminEmail, adminEmail },
                { SetupConstants.AdminPassword, AdminPassword.GetOrDefault(context)?.Trim() ?? string.Empty },
                { SetupConstants.SiteTimeZone, context.GetRequiredService<IClock>().GetSystemTimeZone().TimeZoneId },
                { SetupConstants.DatabaseProvider, Or(DatabaseProvider.GetOrDefault(context), shellSettings["DatabaseProvider"]) },
                { SetupConstants.DatabaseConnectionString, Or(DatabaseConnectionString.GetOrDefault(context), shellSettings["ConnectionString"]) },
                { SetupConstants.DatabaseTablePrefix, Or(DatabaseTablePrefix.GetOrDefault(context), shellSettings["TablePrefix"]) },
                { SetupConstants.DatabaseSchema, Or(DatabaseSchema.GetOrDefault(context), shellSettings["Schema"]) },
            },
        };

        await setupService.SetupAsync(setupContext);
        if (setupContext.Errors.Count > 0)
        {
            await FailAsync(context, string.Join("; ", setupContext.Errors.Select(error => $"{error.Key}: {error.Value}")));
            return;
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    private static string Or(string? value, string? fallback)
    {
        value = value?.Trim();
        return string.IsNullOrEmpty(value) ? fallback ?? string.Empty : value;
    }
}

/// <summary>Enables a disabled tenant.</summary>
[Activity("Crest.Tenants", "Tenant", "Enables a disabled tenant.", DisplayName = "Enable tenant")]
[FlowNode("Done", "Failed")]
public class EnableTenant : TenantActivityBase
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var tenantName = await ResolveTenantNameAsync(context);
        if (tenantName is null)
        {
            return;
        }

        var shellHost = context.GetRequiredService<IShellHost>();
        if (!shellHost.TryGetSettings(tenantName, out var shellSettings) || shellSettings.IsDefaultShell())
        {
            await FailAsync(context, $"No tenant '{tenantName}' to enable.");
            return;
        }

        if (!shellSettings.IsDisabled())
        {
            await FailAsync(context, $"Tenant '{tenantName}' is not disabled.");
            return;
        }

        await shellHost.UpdateShellSettingsAsync(shellSettings.AsRunning());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Disables a running tenant.</summary>
[Activity("Crest.Tenants", "Tenant", "Disables a running tenant.", DisplayName = "Disable tenant")]
[FlowNode("Done", "Failed")]
public class DisableTenant : TenantActivityBase
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var tenantName = await ResolveTenantNameAsync(context);
        if (tenantName is null)
        {
            return;
        }

        var shellHost = context.GetRequiredService<IShellHost>();
        if (!shellHost.TryGetSettings(tenantName, out var shellSettings) || shellSettings.IsDefaultShell())
        {
            await FailAsync(context, $"No tenant '{tenantName}' to disable.");
            return;
        }

        if (!shellSettings.IsRunning())
        {
            await FailAsync(context, $"Tenant '{tenantName}' is not running.");
            return;
        }

        await shellHost.UpdateShellSettingsAsync(shellSettings.AsDisabled());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>What Tenants registers with the workflow registry.</summary>
public sealed class TenantsWorkflowProvider : IWorkflowActivityProvider
{
    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("tenants.create", "Create tenant", WorkflowsConstants.Objects.Tenant, "Crest.Tenants.CreateTenant", "Creates a tenant's settings, uninitialized, from the default tenant.", 10, Category: "Tenant"),
        new("tenants.setup", "Setup tenant", WorkflowsConstants.Objects.Tenant, "Crest.Tenants.SetupTenant", "Runs setup on an uninitialized tenant.", 20, Category: "Tenant"),
        new("tenants.enable", "Enable tenant", WorkflowsConstants.Objects.Tenant, "Crest.Tenants.EnableTenant", "Enables a disabled tenant.", 30, Category: "Tenant"),
        new("tenants.disable", "Disable tenant", WorkflowsConstants.Objects.Tenant, "Crest.Tenants.DisableTenant", "Disables a running tenant.", 40, Category: "Tenant"),
    ];
}
