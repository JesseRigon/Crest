#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OpenIddict.Abstractions;
using Crest.Environment.Shell;
using Crest.Modules;
using Crest.OpenId.Abstractions.Descriptors;
using Crest.OpenId.Abstractions.Managers;
using Crest.OpenId.Services;
using Crest.OpenId.Settings;
using Crest.Security.Services;

namespace Crest.OpenId.Controllers;

// The OpenID Connect administration as JSON (step 7: the MVC Application, Scope,
// ServerConfiguration and ValidationConfiguration screens are retired; the native admin pages
// in Crest.AdminTheme.Client call these). The protocol endpoints (AccessController,
// UserInfoController) are untouched. Each controller belongs to its feature, so a disabled
// feature has no API. Wire shapes are the admin client's (Crest.AdminTheme.Api.OpenIdAdminApi).

/// <summary>One application in the list.</summary>
public sealed record OpenIdApplicationListItem(string Id, string? ClientId, string? DisplayName, string? Type);

/// <summary>
/// An application as the editor reads and writes it. The client secret is write-only: it is
/// never read back (<see cref="HasClientSecret"/> says whether one is stored), and an edit keeps
/// the stored secret unless a new one is sent.
/// </summary>
public sealed class OpenIdApplicationModel
{
    public string? Id { get; set; }

    public string? ClientId { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>"confidential" or "public".</summary>
    public string? Type { get; set; }

    /// <summary>"explicit", "implicit", "external" or "systematic".</summary>
    public string? ConsentType { get; set; }

    public string? ClientSecret { get; set; }

    public bool HasClientSecret { get; set; }

    /// <summary>Space-separated absolute URIs.</summary>
    public string? RedirectUris { get; set; }

    /// <summary>Space-separated absolute URIs.</summary>
    public string? PostLogoutRedirectUris { get; set; }

    public bool AllowAuthorizationCodeFlow { get; set; }

    public bool AllowClientCredentialsFlow { get; set; }

    public bool AllowHybridFlow { get; set; }

    public bool AllowImplicitFlow { get; set; }

    public bool AllowPasswordFlow { get; set; }

    public bool AllowRefreshTokenFlow { get; set; }

    public bool AllowLogoutEndpoint { get; set; }

    public bool AllowIntrospectionEndpoint { get; set; }

    public bool AllowRevocationEndpoint { get; set; }

    public bool RequireProofKeyForCodeExchange { get; set; }

    public bool RequirePushedAuthorizationRequests { get; set; }

    public string[] Roles { get; set; } = [];

    public string[] Scopes { get; set; } = [];
}

/// <summary>The editor: the application (null when creating) and the choices it offers.</summary>
public sealed record OpenIdApplicationEditor(OpenIdApplicationModel? Application, string[] Roles, string[] Scopes, OpenIdServerFlows? ServerFlows);

/// <summary>What the server allows, so the editor offers only what can work (null: Server is off).</summary>
public sealed record OpenIdServerFlows(bool AuthorizationCode, bool ClientCredentials, bool Hybrid, bool Implicit, bool Password, bool RefreshToken, bool Logout, bool Introspection, bool Revocation);

/// <summary>A refusal, keyed by the field it concerns ("" for the whole form).</summary>
public sealed record OpenIdFieldError(string Key, string Message);

public sealed record OpenIdSaveResult(bool Succeeded, OpenIdFieldError[] Errors, string? Id = null);

public sealed record OpenIdScopeModel(string? Id, string? Name, string? DisplayName, string? Description, string? Resources);

/// <summary>
/// The server configuration as a structured form: every setting the retired MVC editor showed,
/// endpoints as on/off (their paths are fixed), certificates by store and thumbprint.
/// </summary>
public sealed class OpenIdServerConfigurationForm
{
    /// <summary>"JsonWebToken" or "DataProtection".</summary>
    public string AccessTokenFormat { get; set; } = nameof(OpenIdServerSettings.TokenFormat.JsonWebToken);

    public string? Authority { get; set; }

    public bool DisableAccessTokenEncryption { get; set; }

    public string? EncryptionCertificateStoreLocation { get; set; }

    public string? EncryptionCertificateStoreName { get; set; }

    public string? EncryptionCertificateThumbprint { get; set; }

    public string? SigningCertificateStoreLocation { get; set; }

    public string? SigningCertificateStoreName { get; set; }

    public string? SigningCertificateThumbprint { get; set; }

    public bool EnableAuthorizationEndpoint { get; set; }

    public bool EnableLogoutEndpoint { get; set; }

    public bool EnableTokenEndpoint { get; set; }

    public bool EnableUserInfoEndpoint { get; set; }

    public bool EnableIntrospectionEndpoint { get; set; }

    public bool EnablePushedAuthorizationEndpoint { get; set; }

    public bool EnableRevocationEndpoint { get; set; }

    public bool AllowAuthorizationCodeFlow { get; set; }

    public bool AllowClientCredentialsFlow { get; set; }

    public bool AllowHybridFlow { get; set; }

    public bool AllowImplicitFlow { get; set; }

    public bool AllowPasswordFlow { get; set; }

    public bool AllowRefreshTokenFlow { get; set; }

    public bool DisableRollingRefreshTokens { get; set; }

    public bool UseReferenceAccessTokens { get; set; }

    public bool RequireProofKeyForCodeExchange { get; set; }

    public bool RequirePushedAuthorizationRequests { get; set; }

    public bool RequireEndSessionConfirmation { get; set; } = true;

    /// <summary>Read only: the certificates the server can use, to pick from.</summary>
    public OpenIdCertificate[] AvailableCertificates { get; set; } = [];
}

public sealed record OpenIdCertificate(string StoreLocation, string StoreName, string? FriendlyName, string Subject, string Issuer, DateTime NotBefore, DateTime NotAfter, string Thumbprint, bool HasPrivateKey);

/// <summary>Token validation, as a structured form.</summary>
public sealed class OpenIdValidationConfigurationForm
{
    public string? Authority { get; set; }

    public string? MetadataAddress { get; set; }

    public string? Audience { get; set; }

    public bool DisableTokenTypeValidation { get; set; }

    /// <summary>The local tenant whose server issues the tokens, instead of an authority.</summary>
    public string? Tenant { get; set; }
}

[ApiController]
[AutoValidateAntiforgeryToken]
[Feature(OpenIdConstants.Features.Management)]
[Route("api/crest/openid/applications")]
public sealed class OpenIdApplicationsApiController(
    IAuthorizationService authorization,
    IOpenIdApplicationManager applications,
    IOpenIdScopeManager scopes,
    IStringLocalizer<OpenIdApplicationsApiController> localizer) : ControllerBase
{
    private readonly IStringLocalizer S = localizer;

    [HttpGet]
    public async Task<ActionResult<OpenIdApplicationListItem[]>> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageApplications))
        {
            return Forbid();
        }


        var items = new List<OpenIdApplicationListItem>();
        await foreach (var application in applications.ListAsync(null, null, HttpContext.RequestAborted))
        {
            items.Add(new OpenIdApplicationListItem(
                await applications.GetPhysicalIdAsync(application),
                await applications.GetClientIdAsync(application),
                await applications.GetDisplayNameAsync(application),
                await applications.GetClientTypeAsync(application)));
        }

        return Ok(items.OrderBy(item => item.DisplayName ?? item.ClientId, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>The editor for a new application (no id) or an existing one.</summary>
    [HttpGet("editor")]
    public async Task<ActionResult<OpenIdApplicationEditor>> GetEditorAsync([FromQuery] string? id = null)
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageApplications))
        {
            return Forbid();
        }


        OpenIdApplicationModel? model = null;
        if (!string.IsNullOrEmpty(id))
        {
            var application = await applications.FindByPhysicalIdAsync(id);
            if (application is null)
            {
                return NotFound();
            }

            model = await ReadAsync(application);
        }

        var roles = HttpContext.RequestServices.GetService<IRoleService>() is { } roleService
            ? (await roleService.GetRoleNamesAsync()).Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        var scopeNames = new List<string>();
        await foreach (var scope in scopes.ListAsync(null, null, HttpContext.RequestAborted))
        {
            if (await scopes.GetNameAsync(scope) is { } name)
            {
                scopeNames.Add(name);
            }
        }

        var flows = HttpContext.RequestServices.GetService<IOpenIdServerService>() is { } server
            ? Flows(await server.GetSettingsAsync())
            : null;

        return Ok(new OpenIdApplicationEditor(model, roles, [.. scopeNames.Order(StringComparer.OrdinalIgnoreCase)], flows));
    }

    [HttpPost]
    public async Task<ActionResult<OpenIdSaveResult>> CreateAsync([FromBody] OpenIdApplicationModel model)
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageApplications))
        {
            return Forbid();
        }


        var errors = new List<OpenIdFieldError>();
        if (string.IsNullOrWhiteSpace(model.ClientId))
        {
            errors.Add(new(nameof(model.ClientId), S["The client identifier is required."]));
        }
        else if (await applications.FindByClientIdAsync(model.ClientId) is not null)
        {
            errors.Add(new(nameof(model.ClientId), S["The client identifier is already taken by another application."]));
        }

        if (IsPublic(model) && !string.IsNullOrEmpty(model.ClientSecret))
        {
            errors.Add(new(nameof(model.ClientSecret), S["No client secret can be set for public applications."]));
        }
        else if (IsConfidential(model) && string.IsNullOrEmpty(model.ClientSecret))
        {
            errors.Add(new(nameof(model.ClientSecret), S["The client secret is required for confidential applications."]));
        }

        errors.AddRange(ValidateUris(model));
        if (errors.Count > 0)
        {
            return BadRequest(new OpenIdSaveResult(false, [.. errors]));
        }


        await applications.UpdateDescriptorFromSettings(ToSettings(model));
        var created = await applications.FindByClientIdAsync(model.ClientId!);
        return Ok(new OpenIdSaveResult(true, [], created is null ? null : await applications.GetPhysicalIdAsync(created)));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<OpenIdSaveResult>> EditAsync(string id, [FromBody] OpenIdApplicationModel model)
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageApplications))
        {
            return Forbid();
        }


        var application = await applications.FindByPhysicalIdAsync(id);
        if (application is null)
        {
            return NotFound();
        }


        var errors = new List<OpenIdFieldError>();
        if (string.IsNullOrWhiteSpace(model.ClientId))
        {
            errors.Add(new(nameof(model.ClientId), S["The client identifier is required."]));
        }
        else if (await applications.FindByClientIdAsync(model.ClientId) is { } other
            && !string.Equals(await applications.GetIdAsync(other), await applications.GetIdAsync(application), StringComparison.Ordinal))
        {
            errors.Add(new(nameof(model.ClientId), S["The client identifier is already taken by another application."]));
        }

        // A public client turned confidential needs a secret now; a public client takes none.
        if (string.IsNullOrEmpty(model.ClientSecret) && !IsPublic(model)
            && await applications.HasClientTypeAsync(application, OpenIddictConstants.ClientTypes.Public))
        {
            errors.Add(new(nameof(model.ClientSecret), S["Setting a new client secret is required."]));
        }

        if (IsPublic(model) && !string.IsNullOrEmpty(model.ClientSecret))
        {
            errors.Add(new(nameof(model.ClientSecret), S["No client secret can be set for public applications."]));
        }

        errors.AddRange(ValidateUris(model));
        if (errors.Count > 0)
        {
            return BadRequest(new OpenIdSaveResult(false, [.. errors]));
        }


        await applications.UpdateDescriptorFromSettings(ToSettings(model), application);
        return Ok(new OpenIdSaveResult(true, [], id));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageApplications))
        {
            return Forbid();
        }


        var application = await applications.FindByPhysicalIdAsync(id);
        if (application is null)
        {
            return NotFound();
        }


        await applications.DeleteAsync(application);
        return NoContent();
    }

    private async Task<OpenIdApplicationModel> ReadAsync(object application)
    {
        ValueTask<bool> Has(string permission) => applications.HasPermissionAsync(application, permission);
        ValueTask<bool> Requires(string requirement) => applications.HasRequirementAsync(application, requirement);

        var model = new OpenIdApplicationModel
        {
            Id = await applications.GetPhysicalIdAsync(application),
            ClientId = await applications.GetClientIdAsync(application),
            DisplayName = await applications.GetDisplayNameAsync(application),
            Type = await applications.GetClientTypeAsync(application),
            ConsentType = await applications.GetConsentTypeAsync(application),
            HasClientSecret = await applications.HasClientTypeAsync(application, OpenIddictConstants.ClientTypes.Confidential),
            RedirectUris = string.Join(' ', await applications.GetRedirectUrisAsync(application)),
            PostLogoutRedirectUris = string.Join(' ', await applications.GetPostLogoutRedirectUrisAsync(application)),
            AllowAuthorizationCodeFlow = await Has(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode) && await Has(OpenIddictConstants.Permissions.ResponseTypes.Code),
            AllowClientCredentialsFlow = await Has(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials),
            // The hybrid flow has no grant type of its own: the code and implicit grants together.
            AllowHybridFlow = await Has(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode)
                && await Has(OpenIddictConstants.Permissions.GrantTypes.Implicit)
                && (await Has(OpenIddictConstants.Permissions.ResponseTypes.CodeIdToken)
                    || await Has(OpenIddictConstants.Permissions.ResponseTypes.CodeIdTokenToken)
                    || await Has(OpenIddictConstants.Permissions.ResponseTypes.CodeToken)),
            AllowImplicitFlow = await Has(OpenIddictConstants.Permissions.GrantTypes.Implicit)
                && (await Has(OpenIddictConstants.Permissions.ResponseTypes.IdToken)
                    || await Has(OpenIddictConstants.Permissions.ResponseTypes.IdTokenToken)
                    || await Has(OpenIddictConstants.Permissions.ResponseTypes.Token)),
            AllowPasswordFlow = await Has(OpenIddictConstants.Permissions.GrantTypes.Password),
            AllowRefreshTokenFlow = await Has(OpenIddictConstants.Permissions.GrantTypes.RefreshToken),
            AllowLogoutEndpoint = await Has("ept:logout") || await Has(OpenIddictConstants.Permissions.Endpoints.EndSession),
            AllowIntrospectionEndpoint = await Has(OpenIddictConstants.Permissions.Endpoints.Introspection),
            AllowRevocationEndpoint = await Has(OpenIddictConstants.Permissions.Endpoints.Revocation),
            RequireProofKeyForCodeExchange = await Requires(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange),
            RequirePushedAuthorizationRequests = await Requires(OpenIddictConstants.Requirements.Features.PushedAuthorizationRequests),
            Roles = [.. await applications.GetRolesAsync(application)],
        };

        var scopeNames = new List<string>();
        await foreach (var scope in scopes.ListAsync(null, null, HttpContext.RequestAborted))
        {
            var name = await scopes.GetNameAsync(scope);
            if (name is not null && await Has(OpenIddictConstants.Permissions.Prefixes.Scope + name))
            {
                scopeNames.Add(name);
            }
        }

        model.Scopes = [.. scopeNames];
        return model;
    }

    private IEnumerable<OpenIdFieldError> ValidateUris(OpenIdApplicationModel model)
    {
        foreach (var (key, value) in new[] { (nameof(model.RedirectUris), model.RedirectUris), (nameof(model.PostLogoutRedirectUris), model.PostLogoutRedirectUris) })
        {
            foreach (var uri in (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || !string.IsNullOrEmpty(parsed.Fragment))
                {
                    yield return new(key, S["'{0}' is not a valid absolute URI without a fragment.", uri]);
                }
            }
        }
    }

    public static OpenIdApplicationSettings ToSettings(OpenIdApplicationModel model) => new()
    {
        AllowAuthorizationCodeFlow = model.AllowAuthorizationCodeFlow,
        AllowClientCredentialsFlow = model.AllowClientCredentialsFlow,
        AllowHybridFlow = model.AllowHybridFlow,
        AllowImplicitFlow = model.AllowImplicitFlow,
        AllowIntrospectionEndpoint = model.AllowIntrospectionEndpoint,
        AllowLogoutEndpoint = model.AllowLogoutEndpoint,
        AllowPasswordFlow = model.AllowPasswordFlow,
        AllowRefreshTokenFlow = model.AllowRefreshTokenFlow,
        AllowRevocationEndpoint = model.AllowRevocationEndpoint,
        ClientId = model.ClientId?.Trim(),
        // Empty keeps the stored secret on an edit (UpdateDescriptorFromSettings sets it only when given).
        ClientSecret = string.IsNullOrEmpty(model.ClientSecret) ? null : model.ClientSecret,
        ConsentType = model.ConsentType,
        DisplayName = model.DisplayName,
        PostLogoutRedirectUris = model.PostLogoutRedirectUris,
        RedirectUris = model.RedirectUris,
        Roles = model.Roles ?? [],
        Scopes = model.Scopes ?? [],
        Type = model.Type,
        RequireProofKeyForCodeExchange = model.RequireProofKeyForCodeExchange,
        RequirePushedAuthorizationRequests = model.RequirePushedAuthorizationRequests,
    };

    private static bool IsPublic(OpenIdApplicationModel model) =>
        string.Equals(model.Type, OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase);

    private static bool IsConfidential(OpenIdApplicationModel model) =>
        string.Equals(model.Type, OpenIddictConstants.ClientTypes.Confidential, StringComparison.OrdinalIgnoreCase);

    private static OpenIdServerFlows Flows(OpenIdServerSettings settings) => new(
        settings.AllowAuthorizationCodeFlow,
        settings.AllowClientCredentialsFlow,
        settings.AllowHybridFlow,
        settings.AllowImplicitFlow,
        settings.AllowPasswordFlow,
        settings.AllowRefreshTokenFlow,
        settings.LogoutEndpointPath.HasValue,
        settings.IntrospectionEndpointPath.HasValue,
        settings.RevocationEndpointPath.HasValue);
}

[ApiController]
[AutoValidateAntiforgeryToken]
[Feature(OpenIdConstants.Features.Management)]
[Route("api/crest/openid/scopes")]
public sealed class OpenIdScopesApiController(
    IAuthorizationService authorization,
    IOpenIdScopeManager scopes,
    ShellSettings shellSettings,
    IStringLocalizer<OpenIdScopesApiController> localizer) : ControllerBase
{
    private readonly IStringLocalizer S = localizer;

    [HttpGet]
    public async Task<ActionResult<OpenIdScopeModel[]>> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageScopes))
        {
            return Forbid();
        }


        var items = new List<OpenIdScopeModel>();
        await foreach (var scope in scopes.ListAsync(null, null, HttpContext.RequestAborted))
        {
            items.Add(await ReadAsync(scope));
        }

        return Ok(items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OpenIdScopeModel>> GetAsync(string id)
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageScopes))
        {
            return Forbid();
        }


        return await scopes.FindByPhysicalIdAsync(id) is { } scope ? Ok(await ReadAsync(scope)) : NotFound();
    }

    [HttpPost]
    public async Task<ActionResult<OpenIdSaveResult>> CreateAsync([FromBody] OpenIdScopeModel model)
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageScopes))
        {
            return Forbid();
        }


        var errors = await ValidateAsync(model, existing: null);
        if (errors.Count > 0)
        {
            return BadRequest(new OpenIdSaveResult(false, [.. errors]));
        }


        var descriptor = new OpenIdScopeDescriptor { Name = model.Name!.Trim(), DisplayName = model.DisplayName, Description = model.Description };
        descriptor.Resources.UnionWith(SplitResources(model.Resources));
        var created = await scopes.CreateAsync(descriptor);
        return Ok(new OpenIdSaveResult(true, [], await scopes.GetPhysicalIdAsync(created)));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<OpenIdSaveResult>> EditAsync(string id, [FromBody] OpenIdScopeModel model)
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageScopes))
        {
            return Forbid();
        }


        var scope = await scopes.FindByPhysicalIdAsync(id);
        if (scope is null)
        {
            return NotFound();
        }


        var errors = await ValidateAsync(model, scope);
        if (errors.Count > 0)
        {
            return BadRequest(new OpenIdSaveResult(false, [.. errors]));
        }


        var descriptor = new OpenIdScopeDescriptor();
        await scopes.PopulateAsync(descriptor, scope);
        descriptor.Name = model.Name!.Trim();
        descriptor.DisplayName = model.DisplayName;
        descriptor.Description = model.Description;
        descriptor.Resources.Clear();
        descriptor.Resources.UnionWith(SplitResources(model.Resources));
        await scopes.UpdateAsync(scope, descriptor);
        return Ok(new OpenIdSaveResult(true, [], id));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageScopes))
        {
            return Forbid();
        }


        var scope = await scopes.FindByPhysicalIdAsync(id);
        if (scope is null)
        {
            return NotFound();
        }


        await scopes.DeleteAsync(scope);
        return NoContent();
    }

    private async Task<OpenIdScopeModel> ReadAsync(object scope) => new(
        await scopes.GetPhysicalIdAsync(scope),
        await scopes.GetNameAsync(scope),
        await scopes.GetDisplayNameAsync(scope),
        await scopes.GetDescriptionAsync(scope),
        string.Join(' ', (await scopes.GetResourcesAsync(scope)).Where(resource => !string.IsNullOrEmpty(resource))));

    private async Task<List<OpenIdFieldError>> ValidateAsync(OpenIdScopeModel model, object? existing)
    {
        var errors = new List<OpenIdFieldError>();
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            errors.Add(new(nameof(model.Name), S["The name is required."]));
        }
        else if (await scopes.FindByNameAsync(model.Name.Trim()) is { } other
            && (existing is null || !string.Equals(await scopes.GetIdAsync(other), await scopes.GetIdAsync(existing), StringComparison.Ordinal)))
        {
            errors.Add(new(nameof(model.Name), S["The name is already taken by another scope."]));
        }

        // The tenant's own resource is added by the server; a scope may not claim it.
        var tenantResource = OpenIdConstants.Prefixes.Tenant + shellSettings.Name;
        if (model.Resources?.Contains(tenantResource, StringComparison.OrdinalIgnoreCase) == true)
        {
            errors.Add(new(nameof(model.Resources), S["The resources field cannot contain the value: {0}.", tenantResource]));
        }

        return errors;
    }

    private static IEnumerable<string> SplitResources(string? resources) =>
        (resources ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

[ApiController]
[AutoValidateAntiforgeryToken]
[Feature(OpenIdConstants.Features.Server)]
[Route("api/crest/openid/server")]
public sealed class OpenIdServerConfigurationApiController(
    IAuthorizationService authorization,
    IOpenIdServerService server,
    IShellHost shellHost,
    ShellSettings shellSettings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OpenIdServerConfigurationForm>> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageServerSettings))
        {
            return Forbid();
        }


        var model = ToModel(await server.GetSettingsAsync());
        model.AvailableCertificates =
        [
            .. (await server.GetAvailableCertificatesAsync()).Select(entry => new OpenIdCertificate(
                entry.location.ToString(),
                entry.name.ToString(),
                entry.certificate.FriendlyName,
                entry.certificate.Subject,
                entry.certificate.Issuer,
                entry.certificate.NotBefore,
                entry.certificate.NotAfter,
                entry.certificate.Thumbprint,
                entry.certificate.HasPrivateKey)),
        ];
        return Ok(model);
    }

    /// <summary>Validates and saves; the tenant is released so the server picks the change up.</summary>
    [HttpPut]
    public async Task<ActionResult<OpenIdSaveResult>> SaveAsync([FromBody] OpenIdServerConfigurationForm model)
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageServerSettings))
        {
            return Forbid();
        }


        var settings = await server.GetSettingsAsync();
        var errors = Apply(model, settings);
        if (errors.Count == 0)
        {
            errors.AddRange((await server.ValidateSettingsAsync(settings))
                .Where(result => result != ValidationResult.Success)
                .Select(result => new OpenIdFieldError(result.MemberNames.FirstOrDefault() ?? string.Empty, result.ErrorMessage ?? string.Empty)));
        }

        if (errors.Count > 0)

        {

            return BadRequest(new OpenIdSaveResult(false, [.. errors]));

        }


        await server.UpdateSettingsAsync(settings);
        await shellHost.ReleaseShellContextAsync(shellSettings);
        return Ok(new OpenIdSaveResult(true, []));
    }

    public static OpenIdServerConfigurationForm ToModel(OpenIdServerSettings settings) => new()
    {
        AccessTokenFormat = settings.AccessTokenFormat.ToString(),
        Authority = settings.Authority?.AbsoluteUri,
        DisableAccessTokenEncryption = settings.DisableAccessTokenEncryption,
        EncryptionCertificateStoreLocation = settings.EncryptionCertificateStoreLocation?.ToString(),
        EncryptionCertificateStoreName = settings.EncryptionCertificateStoreName?.ToString(),
        EncryptionCertificateThumbprint = settings.EncryptionCertificateThumbprint,
        SigningCertificateStoreLocation = settings.SigningCertificateStoreLocation?.ToString(),
        SigningCertificateStoreName = settings.SigningCertificateStoreName?.ToString(),
        SigningCertificateThumbprint = settings.SigningCertificateThumbprint,
        EnableAuthorizationEndpoint = settings.AuthorizationEndpointPath.HasValue,
        EnableLogoutEndpoint = settings.LogoutEndpointPath.HasValue,
        EnableTokenEndpoint = settings.TokenEndpointPath.HasValue,
        EnableUserInfoEndpoint = settings.UserinfoEndpointPath.HasValue,
        EnableIntrospectionEndpoint = settings.IntrospectionEndpointPath.HasValue,
        EnablePushedAuthorizationEndpoint = settings.PushedAuthorizationEndpointPath.HasValue,
        EnableRevocationEndpoint = settings.RevocationEndpointPath.HasValue,
        AllowAuthorizationCodeFlow = settings.AllowAuthorizationCodeFlow,
        AllowClientCredentialsFlow = settings.AllowClientCredentialsFlow,
        AllowHybridFlow = settings.AllowHybridFlow,
        AllowImplicitFlow = settings.AllowImplicitFlow,
        AllowPasswordFlow = settings.AllowPasswordFlow,
        AllowRefreshTokenFlow = settings.AllowRefreshTokenFlow,
        DisableRollingRefreshTokens = settings.DisableRollingRefreshTokens,
        UseReferenceAccessTokens = settings.UseReferenceAccessTokens,
        RequireProofKeyForCodeExchange = settings.RequireProofKeyForCodeExchange,
        RequirePushedAuthorizationRequests = settings.RequirePushedAuthorizationRequests,
        RequireEndSessionConfirmation = settings.RequireEndSessionConfirmation,
    };

    /// <summary>Applies the form to the settings; the refusals of values that do not parse.</summary>
    public static List<OpenIdFieldError> Apply(OpenIdServerConfigurationForm model, OpenIdServerSettings settings)
    {
        var errors = new List<OpenIdFieldError>();
        if (Enum.TryParse<OpenIdServerSettings.TokenFormat>(model.AccessTokenFormat, ignoreCase: true, out var format))
        {
            settings.AccessTokenFormat = format;
        }
        else
        {
            errors.Add(new(nameof(model.AccessTokenFormat), $"'{model.AccessTokenFormat}' is not a token format."));
        }

        if (string.IsNullOrWhiteSpace(model.Authority))
        {
            settings.Authority = null;
        }
        else if (Uri.TryCreate(model.Authority, UriKind.Absolute, out var authority))
        {
            settings.Authority = authority;
        }
        else
        {
            errors.Add(new(nameof(model.Authority), "The authority must be an absolute URI."));
        }

        settings.EncryptionCertificateStoreLocation = ParseOrNull<StoreLocation>(model.EncryptionCertificateStoreLocation);
        settings.EncryptionCertificateStoreName = ParseOrNull<StoreName>(model.EncryptionCertificateStoreName);
        settings.EncryptionCertificateThumbprint = model.EncryptionCertificateThumbprint;
        settings.SigningCertificateStoreLocation = ParseOrNull<StoreLocation>(model.SigningCertificateStoreLocation);
        settings.SigningCertificateStoreName = ParseOrNull<StoreName>(model.SigningCertificateStoreName);
        settings.SigningCertificateThumbprint = model.SigningCertificateThumbprint;
        settings.DisableAccessTokenEncryption = model.DisableAccessTokenEncryption;

        // The endpoint paths are fixed; a form switches them on or off, as the MVC editor did.
        settings.AuthorizationEndpointPath = model.EnableAuthorizationEndpoint ? new PathString("/connect/authorize") : PathString.Empty;
        settings.LogoutEndpointPath = model.EnableLogoutEndpoint ? new PathString("/connect/logout") : PathString.Empty;
        settings.TokenEndpointPath = model.EnableTokenEndpoint ? new PathString("/connect/token") : PathString.Empty;
        settings.UserinfoEndpointPath = model.EnableUserInfoEndpoint ? new PathString("/connect/userinfo") : PathString.Empty;
        settings.IntrospectionEndpointPath = model.EnableIntrospectionEndpoint ? new PathString("/connect/introspect") : PathString.Empty;
        settings.PushedAuthorizationEndpointPath = model.EnablePushedAuthorizationEndpoint ? new PathString("/connect/par") : PathString.Empty;
        settings.RevocationEndpointPath = model.EnableRevocationEndpoint ? new PathString("/connect/revoke") : PathString.Empty;

        settings.AllowAuthorizationCodeFlow = model.AllowAuthorizationCodeFlow;
        settings.AllowClientCredentialsFlow = model.AllowClientCredentialsFlow;
        settings.AllowHybridFlow = model.AllowHybridFlow;
        settings.AllowImplicitFlow = model.AllowImplicitFlow;
        settings.AllowPasswordFlow = model.AllowPasswordFlow;
        settings.AllowRefreshTokenFlow = model.AllowRefreshTokenFlow;
        settings.DisableRollingRefreshTokens = model.DisableRollingRefreshTokens;
        settings.UseReferenceAccessTokens = model.UseReferenceAccessTokens;
        settings.RequireProofKeyForCodeExchange = model.RequireProofKeyForCodeExchange;
        settings.RequirePushedAuthorizationRequests = model.RequirePushedAuthorizationRequests;
        settings.RequireEndSessionConfirmation = model.RequireEndSessionConfirmation;
        return errors;
    }

    private static T? ParseOrNull<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : null;
}

[ApiController]
[AutoValidateAntiforgeryToken]
[Feature(OpenIdConstants.Features.Validation)]
[Route("api/crest/openid/validation")]
public sealed class OpenIdValidationConfigurationApiController(
    IAuthorizationService authorization,
    IOpenIdValidationService validation,
    IShellHost shellHost,
    ShellSettings shellSettings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OpenIdValidationConfigurationForm>> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageValidationSettings))
        {
            return Forbid();
        }


        var settings = await validation.GetSettingsAsync();
        return Ok(new OpenIdValidationConfigurationForm
        {
            Authority = settings.Authority?.AbsoluteUri,
            MetadataAddress = settings.MetadataAddress?.AbsoluteUri,
            Audience = settings.Audience,
            DisableTokenTypeValidation = settings.DisableTokenTypeValidation,
            Tenant = settings.Tenant,
        });
    }

    [HttpPut]
    public async Task<ActionResult<OpenIdSaveResult>> SaveAsync([FromBody] OpenIdValidationConfigurationForm model)
    {
        if (!await authorization.AuthorizeAsync(User, OpenIdPermissions.ManageValidationSettings))
        {
            return Forbid();
        }


        var errors = new List<OpenIdFieldError>();
        var settings = await validation.GetSettingsAsync();
        settings.Authority = ParseUri(model.Authority, nameof(model.Authority), errors);
        settings.MetadataAddress = ParseUri(model.MetadataAddress, nameof(model.MetadataAddress), errors);
        settings.Audience = model.Audience?.Trim();
        settings.DisableTokenTypeValidation = model.DisableTokenTypeValidation;
        settings.Tenant = string.IsNullOrWhiteSpace(model.Tenant) ? null : model.Tenant.Trim();

        if (errors.Count == 0)
        {
            errors.AddRange((await validation.ValidateSettingsAsync(settings))
                .Where(result => result != ValidationResult.Success)
                .Select(result => new OpenIdFieldError(result.MemberNames.FirstOrDefault() ?? string.Empty, result.ErrorMessage ?? string.Empty)));
        }

        if (errors.Count > 0)

        {

            return BadRequest(new OpenIdSaveResult(false, [.. errors]));

        }


        await validation.UpdateSettingsAsync(settings);
        await shellHost.ReleaseShellContextAsync(shellSettings);
        return Ok(new OpenIdSaveResult(true, []));
    }

    private static Uri? ParseUri(string? value, string key, List<OpenIdFieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return uri;
        }

        errors.Add(new(key, "The value must be an absolute URI."));
        return null;
    }
}
