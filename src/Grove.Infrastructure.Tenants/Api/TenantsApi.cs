#nullable enable

using System.Security.Claims;
using Grove.Api;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Removing;
using Grove.Infrastructure.Tenants.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Infrastructure.Tenants.Api;

/// <summary>
/// The tenants page's API (<c>api/grove/tenants</c>): list, create, edit, set up, enable and
/// disable, reload and remove. Answered in the default tenant only (the feature is
/// default-tenant-only, and every call checks it). The rules are <see cref="TenantAdministration"/>'s,
/// shared with the stock tenants API.
/// </summary>
public sealed class TenantsApi : ApiContributor
{
    public override string Name => "Tenants";

    public override string FeatureId => "Grove.Infrastructure.Tenants";

    public override void Map(IEndpointRouteBuilder group)
    {
        var tenants = group.MapGroup("tenants");
        tenants.MapGet("", ListAsync);
        tenants.MapGet("editor", GetEditorAsync);
        tenants.MapPost("", CreateAsync);
        tenants.MapPut("{name}", EditAsync);
        tenants.MapPost("{name}/setup", SetupAsync);
        tenants.MapGet("{name}/setup-url", GetSetupUrlAsync);
        tenants.MapPost("{name}/enable", ([FromRoute] string name, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess) => ChangeStateAsync(name, "Enable", user, requestAccess));
        tenants.MapPost("{name}/disable", ([FromRoute] string name, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess) => ChangeStateAsync(name, "Disable", user, requestAccess));
        tenants.MapPost("bulk", BulkAsync);
        tenants.MapPost("{name}/reload", ReloadAsync);
        tenants.MapDelete("{name}", RemoveAsync);
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        [FromServices] IRequestAccess requestAccess,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? state = null,
        [FromQuery] string? orderBy = null)
    {
        var access = await GetDefaultTenantAccessAsync(user, requestAccess);
        if (access is null) return TypedResults.Forbid();

        var settings = access.GetRequiredService<IShellHost>().GetAllSettings();
        var entries = settings.Select(ToTenant);
        if (!string.IsNullOrWhiteSpace(search))
        {
            entries = entries.Where(tenant => tenant.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || tenant.RequestUrlHost?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                || tenant.RequestUrlPrefix?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        }
        if (!string.IsNullOrWhiteSpace(category)) entries = entries.Where(tenant => string.Equals(tenant.Category, category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(state) && !string.Equals(state, "All", StringComparison.OrdinalIgnoreCase)) entries = entries.Where(tenant => string.Equals(tenant.State, state, StringComparison.OrdinalIgnoreCase));

        entries = string.Equals(orderBy, "State", StringComparison.OrdinalIgnoreCase)
            ? entries.OrderBy(tenant => tenant.State, StringComparer.OrdinalIgnoreCase).ThenBy(tenant => tenant.Name, StringComparer.OrdinalIgnoreCase)
            : entries.OrderBy(tenant => tenant.Name, StringComparer.OrdinalIgnoreCase);

        var categories = settings.Select(setting => setting["Category"])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var removalAllowed = access.GetRequiredService<IOptions<TenantsOptions>>().Value.TenantRemovalAllowed;
        return TypedResults.Ok(new TenantCatalog(entries.ToArray(), categories, removalAllowed));
    }

    // Create, edit and setup (step 7: the MVC Tenants Create/Edit screens are retired). The rules
    // are TenantAdministration's, shared with the stock tenants API.
    private static async Task<IResult> GetEditorAsync(ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess, [FromQuery] string? name = null)
    {
        var access = await GetDefaultTenantAccessAsync(user, requestAccess);
        if (access is null) return TypedResults.Forbid();

        var state = await access.GetRequiredService<TenantAdministration>().GetEditorAsync(name);
        if (state is null) return TypedResults.NotFound();

        return TypedResults.Ok(new TenantEditor(
            ToWire(state.Form),
            state.IsNew,
            state.IsUninitialized,
            state.CanEditDatabase,
            state.DatabaseProviderPreset,
            state.DatabaseConfigurationPreset,
            state.RequireTablePrefix,
            state.HasTablePrefixPattern,
            state.HasSchemaPattern,
            state.ResolvedTablePrefix,
            state.ResolvedSchema,
            [.. state.Recipes.Select(ToChoice)],
            [.. state.FeatureProfiles.Select(ToChoice)],
            [.. state.DatabaseProviders.Select(ToChoice)]));
    }

    private static async Task<IResult> CreateAsync([FromBody] TenantFormModel form, HttpContext context, [FromServices] IRequestAccess requestAccess, [FromQuery] bool setupUrl = false)
    {
        var access = await GetDefaultTenantAccessAsync(context.User, requestAccess);
        if (access is null) return TypedResults.Forbid();

        var administration = access.GetRequiredService<TenantAdministration>();
        var result = await administration.CreateAsync(FromWire(form));
        return ToResponse(result, setupUrl && result.Settings is { } created ? administration.SetupUrl(context, created.Name) : null);
    }

    private static async Task<IResult> EditAsync([FromRoute] string name, [FromBody] TenantFormModel form, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        var access = await GetDefaultTenantAccessAsync(user, requestAccess);
        if (access is null) return TypedResults.Forbid();

        return ToResponse(await access.GetRequiredService<TenantAdministration>().EditAsync(name, FromWire(form)), null);
    }

    private static async Task<IResult> SetupAsync([FromRoute] string name, [FromBody] TenantSetupRequest request, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        var access = await GetDefaultTenantAccessAsync(user, requestAccess);
        if (access is null) return TypedResults.Forbid();

        return ToResponse(await access.GetRequiredService<TenantAdministration>().SetupAsync(name, new TenantSetupForm
        {
            SiteName = request.SiteName,
            UserName = request.UserName,
            Email = request.Email,
            Password = request.Password,
            RecipeName = request.RecipeName,
            SiteTimeZone = request.SiteTimeZone,
            DatabaseProvider = request.DatabaseProvider,
            ConnectionString = request.ConnectionString,
            TablePrefix = request.TablePrefix,
            Schema = request.Schema,
        }), null);
    }

    private static async Task<IResult> GetSetupUrlAsync([FromRoute] string name, HttpContext context, [FromServices] IRequestAccess requestAccess)
    {
        var access = await GetDefaultTenantAccessAsync(context.User, requestAccess);
        if (access is null) return TypedResults.Forbid();

        var url = access.GetRequiredService<TenantAdministration>().SetupUrl(context, name);
        return url is null ? TypedResults.NotFound() : TypedResults.Ok(new { url });
    }

    private static IResult ToResponse(TenantResult result, string? setupUrl)
    {
        var response = new TenantSaveResult(
            result.Succeeded,
            [.. result.Errors.Select(error => new TenantFieldError(error.Key, error.Message))],
            result.Settings is { } settings ? ToTenant(settings) : null,
            setupUrl);
        return result.Succeeded ? TypedResults.Ok(response) : TypedResults.BadRequest(response);
    }

    private static TenantChoice ToChoice(TenantOption option) => new(option.Value, option.Text, option.HasConnectionString);

    private static TenantFormModel ToWire(Grove.Infrastructure.Tenants.Services.TenantForm form) => new()
    {
        Name = form.Name,
        Description = form.Description,
        Category = form.Category,
        RequestUrlHost = form.RequestUrlHost,
        RequestUrlPrefix = form.RequestUrlPrefix,
        DatabaseProvider = form.DatabaseProvider,
        ConnectionString = form.ConnectionString,
        TablePrefix = form.TablePrefix,
        Schema = form.Schema,
        RecipeName = form.RecipeName,
        FeatureProfiles = form.FeatureProfiles ?? [],
    };

    private static Grove.Infrastructure.Tenants.Services.TenantForm FromWire(TenantFormModel form) => new()
    {
        Name = form.Name?.Trim(),
        Description = form.Description,
        Category = form.Category,
        RequestUrlHost = form.RequestUrlHost,
        RequestUrlPrefix = form.RequestUrlPrefix,
        DatabaseProvider = form.DatabaseProvider,
        ConnectionString = form.ConnectionString,
        TablePrefix = form.TablePrefix,
        Schema = form.Schema,
        RecipeName = form.RecipeName,
        FeatureProfiles = form.FeatureProfiles ?? [],
    };

    private static async Task<IResult> BulkAsync([FromBody] TenantBulkAction request, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        if (!string.Equals(request.Action, "Enable", StringComparison.OrdinalIgnoreCase) && !string.Equals(request.Action, "Disable", StringComparison.OrdinalIgnoreCase)) return ApiResults.BadRequest("Only Enable and Disable are valid tenant bulk actions.");
        var access = await GetDefaultTenantAccessAsync(user, requestAccess);
        if (access is null) return TypedResults.Forbid();

        var host = access.GetRequiredService<IShellHost>();
        var updated = new List<TenantSummary>();
        foreach (var name in request.Names?.Distinct(StringComparer.OrdinalIgnoreCase) ?? [])
        {
            if (!host.TryGetSettings(name, out var settings)) continue;
            if (string.Equals(request.Action, "Enable", StringComparison.OrdinalIgnoreCase) && settings.IsDisabled())
            {
                await host.UpdateShellSettingsAsync(settings.AsRunning());
                updated.Add(ToTenant(settings.AsRunning()));
            }
            else if (string.Equals(request.Action, "Disable", StringComparison.OrdinalIgnoreCase) && !settings.IsDefaultShell() && settings.IsRunning())
            {
                await host.UpdateShellSettingsAsync(settings.AsDisabled());
                updated.Add(ToTenant(settings.AsDisabled()));
            }
        }
        return TypedResults.Ok(updated.ToArray());
    }

    private static async Task<IResult> ReloadAsync([FromRoute] string name, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        var access = await GetDefaultTenantAccessAsync(user, requestAccess);
        if (access is null) return TypedResults.Forbid();
        var host = access.GetRequiredService<IShellHost>();
        if (!host.TryGetSettings(name, out var settings)) return TypedResults.NotFound();
        await host.ReloadShellContextAsync(settings);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> RemoveAsync([FromRoute] string name, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        var access = await GetDefaultTenantAccessAsync(user, requestAccess);
        if (access is null) return TypedResults.Forbid();
        if (!access.GetRequiredService<IOptions<TenantsOptions>>().Value.TenantRemovalAllowed) return TypedResults.Forbid();

        var host = access.GetRequiredService<IShellHost>();
        if (!host.TryGetSettings(name, out var settings)) return TypedResults.NotFound();
        if (!settings.IsRemovable()) return ApiResults.BadRequest("Only Disabled or Uninitialized tenants can be removed.");

        var result = await access.GetRequiredService<IShellRemovalManager>().RemoveAsync(settings);
        return result.Success ? TypedResults.NoContent() : ApiResults.BadRequest(result.ErrorMessage ?? string.Empty);
    }

    private static async Task<IResult> ChangeStateAsync(string name, string action, ClaimsPrincipal user, IRequestAccess requestAccess)
    {
        var access = await GetDefaultTenantAccessAsync(user, requestAccess);
        if (access is null) return TypedResults.Forbid();
        var host = access.GetRequiredService<IShellHost>();
        if (!host.TryGetSettings(name, out var settings)) return TypedResults.NotFound();

        if (string.Equals(action, "Enable", StringComparison.OrdinalIgnoreCase))
        {
            if (!settings.IsDisabled()) return ApiResults.BadRequest("Only Disabled tenants can be enabled.");
            var enabled = settings.AsRunning();
            await host.UpdateShellSettingsAsync(enabled);
            return TypedResults.Ok(ToTenant(enabled));
        }

        if (settings.IsDefaultShell()) return ApiResults.BadRequest("The default tenant cannot be disabled.");
        if (!settings.IsRunning()) return ApiResults.BadRequest("Only Running tenants can be disabled.");
        var disabled = settings.AsDisabled();
        await host.UpdateShellSettingsAsync(disabled);
        return TypedResults.Ok(ToTenant(disabled));
    }

    private static async Task<AuthorizedRequest?> GetDefaultTenantAccessAsync(ClaimsPrincipal user, IRequestAccess requestAccess)
    {
        var access = await requestAccess.AuthorizeAsync(user, Permissions.ManageTenants);
        if (access is null || !access.GetRequiredService<ShellSettings>().IsDefaultShell()) return null;
        return access;
    }

    private static TenantSummary ToTenant(ShellSettings settings) => new(
        settings.Name,
        settings.State.ToString(),
        settings["Category"],
        settings["Description"],
        settings.RequestUrlHost,
        settings.RequestUrlPrefix,
        settings.IsDefaultShell(),
        settings.IsRemovable());
}

// The wire models: the same shapes as the admin client's own (Grove.Themes.Admin.Api).
public sealed record TenantSummary(string Name, string State, string? Category, string? Description, string? RequestUrlHost, string? RequestUrlPrefix, bool IsDefault, bool IsRemovable);

public sealed record TenantCatalog(TenantSummary[] Tenants, string[] Categories, bool TenantRemovalAllowed);

public sealed record TenantBulkAction(string Action, string[]? Names);

public sealed class TenantFormModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? RequestUrlHost { get; set; }

    public string? RequestUrlPrefix { get; set; }

    public string? DatabaseProvider { get; set; }

    public string? ConnectionString { get; set; }

    public string? TablePrefix { get; set; }

    public string? Schema { get; set; }

    public string? RecipeName { get; set; }

    public string[] FeatureProfiles { get; set; } = [];
}

public sealed record TenantChoice(string Value, string Text, bool HasConnectionString = false);

public sealed record TenantEditor(
    TenantFormModel Form,
    bool IsNew,
    bool IsUninitialized,
    bool CanEditDatabase,
    bool DatabaseProviderPreset,
    bool DatabaseConfigurationPreset,
    bool RequireTablePrefix,
    bool HasTablePrefixPattern,
    bool HasSchemaPattern,
    string? ResolvedTablePrefix,
    string? ResolvedSchema,
    TenantChoice[] Recipes,
    TenantChoice[] FeatureProfiles,
    TenantChoice[] DatabaseProviders);

public sealed record TenantFieldError(string Key, string Message);

public sealed record TenantSaveResult(bool Succeeded, TenantFieldError[] Errors, TenantSummary? Tenant = null, string? SetupUrl = null);

public sealed class TenantSetupRequest
{
    public string? SiteName { get; set; }

    public string? UserName { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? RecipeName { get; set; }

    public string? SiteTimeZone { get; set; }

    public string? DatabaseProvider { get; set; }

    public string? ConnectionString { get; set; }

    public string? TablePrefix { get; set; }

    public string? Schema { get; set; }
}
