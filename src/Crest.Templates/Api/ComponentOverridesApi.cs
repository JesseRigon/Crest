#nullable enable

using System.Security.Claims;
using Crest.Api;
using Crest.DisplayManagement.Components;
using Crest.Templates.Models;
using Crest.Templates.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Crest.Templates.Api;

/// <summary>
/// Overrides (docs/blazor-display.md § 5a): a variant saves a configured component (or composite,
/// or variant) under a name tenants place like a component, under the shared-components permission
/// since every placement of it inherits the change; an instance override adjusts one placement,
/// under the templates permission. Values are checked against the schema of what they override;
/// events are never overridden, and data-derived values are skipped where they are applied.
/// </summary>
public sealed class ComponentOverridesApi : ApiContributor
{
    public override string Name => "ComponentOverrides";

    public override string FeatureId => "Crest.Templates";

    public override void Map(IEndpointRouteBuilder group)
    {
        var overrides = group.MapGroup("templates/overrides");
        overrides.MapGet("", GetAsync);
        overrides.MapPut("variants/{name}", SaveVariantAsync);
        overrides.MapDelete("variants/{name}", DeleteVariantAsync);
        overrides.MapPut("instances/{instanceId}", SaveInstanceAsync);
        overrides.MapDelete("instances/{instanceId}", DeleteInstanceAsync);
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal user, [FromServices] ComponentOverrides overrides, [FromServices] IAuthorizationService authorization)
    {
        if (!await authorization.AuthorizeAsync(user, Permissions.ManageTemplates))
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Ok(await overrides.GetAsync());
    }

    private static async Task<IResult> SaveVariantAsync(
        [FromRoute] string name,
        [FromBody] ComponentVariant variant,
        ClaimsPrincipal user,
        [FromServices] ComponentOverrides overrides,
        [FromServices] ITemplateCatalog templates,
        [FromServices] IComponentRegistryManager registries,
        [FromServices] IAuthorizationService authorization)
    {
        if (!await authorization.AuthorizeAsync(user, Permissions.ManageSharedComponents))
        {
            return TypedResults.Forbid();
        }

        variant.Name = name.Trim();
        variant.Values ??= [];
        if (variant.Name.Length == 0 || string.Equals(variant.Name, variant.Base, StringComparison.OrdinalIgnoreCase))
        {
            return Errors("A variant needs a name of its own and a base.");
        }

        if (await FindComponentAsync(registries, templates, variant.Name) is not null)
        {
            return Errors($"'{variant.Name}' is already a component; a variant needs a name of its own.");
        }

        var document = await overrides.LoadAsync();
        document.Variants.RemoveAll(existing => string.Equals(existing.Name, variant.Name, StringComparison.OrdinalIgnoreCase));
        document.Variants.Add(variant);

        var resolved = ComponentOverrides.Resolve(document, variant.Name, out var problem);
        if (problem is not null)
        {
            return Errors(problem);
        }

        if (resolved is null || await FindComponentAsync(registries, templates, resolved.Component) is not { } component)
        {
            return Errors($"The base '{variant.Base}' is not a component, a composite or a variant.");
        }

        var errors = ComponentOverrides.Check(component, resolved.Values);
        if (errors.Count > 0)
        {
            return TypedResults.BadRequest(new { errors });
        }

        await overrides.SaveAsync(document);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteVariantAsync([FromRoute] string name, ClaimsPrincipal user, [FromServices] ComponentOverrides overrides, [FromServices] IAuthorizationService authorization)
    {
        if (!await authorization.AuthorizeAsync(user, Permissions.ManageSharedComponents))
        {
            return TypedResults.Forbid();
        }

        var document = await overrides.LoadAsync();
        if (document.Variants.Any(variant => string.Equals(variant.Base, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Errors("Other variants inherit from this one; change their base first.");
        }

        document.Variants.RemoveAll(variant => string.Equals(variant.Name, name, StringComparison.OrdinalIgnoreCase));
        await overrides.SaveAsync(document);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SaveInstanceAsync([FromRoute] string instanceId, [FromBody] InstanceOverride instance, ClaimsPrincipal user, [FromServices] ComponentOverrides overrides, [FromServices] IAuthorizationService authorization)
    {
        if (!await authorization.AuthorizeAsync(user, Permissions.ManageTemplates))
        {
            return TypedResults.Forbid();
        }

        instance.InstanceId = instanceId;
        instance.Values ??= [];
        var document = await overrides.LoadAsync();
        document.Instances.RemoveAll(existing => string.Equals(existing.InstanceId, instanceId, StringComparison.Ordinal));
        if (instance.Values.Count > 0)
        {
            document.Instances.Add(instance);
        }

        await overrides.SaveAsync(document);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteInstanceAsync([FromRoute] string instanceId, ClaimsPrincipal user, [FromServices] ComponentOverrides overrides, [FromServices] IAuthorizationService authorization)
    {
        if (!await authorization.AuthorizeAsync(user, Permissions.ManageTemplates))
        {
            return TypedResults.Forbid();
        }

        var document = await overrides.LoadAsync();
        document.Instances.RemoveAll(instance => string.Equals(instance.InstanceId, instanceId, StringComparison.Ordinal));
        await overrides.SaveAsync(document);
        return TypedResults.NoContent();
    }

    private static IResult Errors(string error) => TypedResults.BadRequest(new { errors = new[] { error } });

    // A coded component of any shell, or a published composite.
    private static async Task<ComponentDescriptor?> FindComponentAsync(IComponentRegistryManager registries, ITemplateCatalog templates, string name)
    {
        foreach (var shell in new[] { ShellKind.Site, ShellKind.Admin, ShellKind.Member })
        {
            if (registries.GetRegistry(shell).TryGet(name, out var component))
            {
                return component;
            }
        }

        return (await templates.ListPublishedAsync()).FirstOrDefault(template => template.IsComposite && string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase)) is { } composite
            ? TemplateComposites.Describe(composite)
            : null;
    }
}
