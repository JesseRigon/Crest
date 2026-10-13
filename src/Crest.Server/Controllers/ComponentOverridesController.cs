using Crest.DisplayManagement.Components;
using Crest.Templates.Models;
using Crest.Templates.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplatesPermissions = Crest.Templates.Permissions;

namespace Crest.Controllers;

/// <summary>
/// Overrides (docs/blazor-display.md § 5a): a variant saves a configured component (or composite,
/// or variant) under a name tenants place like a component, under the shared-components permission
/// since every placement of it inherits the change; an instance override adjusts one placement,
/// under the templates permission. Values are checked against the schema of what they override;
/// events are never overridden, and data-derived values are skipped where they are applied.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/templates/overrides")]
public sealed class ComponentOverridesController(
    ComponentOverrides overrides,
    ITemplateCatalog templates,
    IComponentRegistryManager registries,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ComponentOverridesDocument>> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, TemplatesPermissions.ManageTemplates))
        {
            return Forbid();
        }

        return Ok(await overrides.GetAsync());
    }

    [HttpPut("variants/{name}")]
    public async Task<IActionResult> SaveVariantAsync(string name, [FromBody] ComponentVariant variant)
    {
        if (!await authorization.AuthorizeAsync(User, TemplatesPermissions.ManageSharedComponents))
        {
            return Forbid();
        }

        variant.Name = name.Trim();
        variant.Values ??= [];
        if (variant.Name.Length == 0 || string.Equals(variant.Name, variant.Base, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { errors = new[] { "A variant needs a name of its own and a base." } });
        }

        if (await FindComponentAsync(variant.Name) is not null)
        {
            return BadRequest(new { errors = new[] { $"'{variant.Name}' is already a component; a variant needs a name of its own." } });
        }

        var document = await overrides.LoadAsync();
        document.Variants.RemoveAll(existing => string.Equals(existing.Name, variant.Name, StringComparison.OrdinalIgnoreCase));
        document.Variants.Add(variant);

        var resolved = ComponentOverrides.Resolve(document, variant.Name, out var problem);
        if (problem is not null)
        {
            return BadRequest(new { errors = new[] { problem } });
        }

        if (resolved is null || await FindComponentAsync(resolved.Component) is not { } component)
        {
            return BadRequest(new { errors = new[] { $"The base '{variant.Base}' is not a component, a composite or a variant." } });
        }

        var errors = ComponentOverrides.Check(component, resolved.Values);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        await overrides.SaveAsync(document);
        return NoContent();
    }

    [HttpDelete("variants/{name}")]
    public async Task<IActionResult> DeleteVariantAsync(string name)
    {
        if (!await authorization.AuthorizeAsync(User, TemplatesPermissions.ManageSharedComponents))
        {
            return Forbid();
        }

        var document = await overrides.LoadAsync();
        if (document.Variants.Any(variant => string.Equals(variant.Base, name, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { errors = new[] { "Other variants inherit from this one; change their base first." } });
        }

        document.Variants.RemoveAll(variant => string.Equals(variant.Name, name, StringComparison.OrdinalIgnoreCase));
        await overrides.SaveAsync(document);
        return NoContent();
    }

    [HttpPut("instances/{instanceId}")]
    public async Task<IActionResult> SaveInstanceAsync(string instanceId, [FromBody] InstanceOverride instance)
    {
        if (!await authorization.AuthorizeAsync(User, TemplatesPermissions.ManageTemplates))
        {
            return Forbid();
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
        return NoContent();
    }

    [HttpDelete("instances/{instanceId}")]
    public async Task<IActionResult> DeleteInstanceAsync(string instanceId)
    {
        if (!await authorization.AuthorizeAsync(User, TemplatesPermissions.ManageTemplates))
        {
            return Forbid();
        }

        var document = await overrides.LoadAsync();
        document.Instances.RemoveAll(instance => string.Equals(instance.InstanceId, instanceId, StringComparison.Ordinal));
        await overrides.SaveAsync(document);
        return NoContent();
    }

    // A coded component of any shell, or a published composite.
    private async Task<ComponentDescriptor?> FindComponentAsync(string name)
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
