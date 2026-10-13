using Crest.Admin;
using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Controllers;

/// <summary>
/// The component registry's schema per shell (docs/blazor-display.md § 1: one schema shared by
/// the server, the renderer and the designer): what the Layers page, the Templates page and the
/// designer offer. Schema only, never data (permission set 2); admin users only.
/// </summary>
[ApiController]
[Route("api/crest/components")]
public sealed class ComponentsController(
    IComponentRegistryManager registries,
    Crest.Templates.Services.ITemplateCatalog templates,
    Crest.Templates.Services.ComponentOverrides overrides,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ComponentSchemaModel[]>> ListAsync([FromQuery] string? shell = null)
    {
        if (!await authorization.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel))
        {
            return Forbid();
        }

        var kind = ComponentSchema.ParseShell(shell?.ToLowerInvariant()) ?? ShellKind.Site;

        // Tenants' composites are components too (rulings 2026-10-12): listed with the coded ones,
        // a coded component keeping its name over a composite of the same name.
        var coded = registries.GetRegistry(kind).Components.ToList();
        var composites = (await templates.ListPublishedAsync())
            .Where(template => template.IsComposite && !coded.Any(component => string.Equals(component.Name, template.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(Crest.Templates.Services.TemplateComposites.Describe);

        // Variants (§ 5a) are placed by name too: listed as what they resolve to, under their own name.
        var known = coded.Concat(composites).ToList();
        var document = await overrides.GetAsync();
        var variants = document.Variants
            .Select(variant => (Variant: variant, Resolved: Crest.Templates.Services.ComponentOverrides.Resolve(document, variant.Name, out _)))
            .Where(entry => entry.Resolved is not null && known.Any(component => string.Equals(component.Name, entry.Resolved.Component, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => known.First(component => string.Equals(component.Name, entry.Resolved!.Component, StringComparison.OrdinalIgnoreCase)) with
            {
                Name = entry.Variant.Name,
                Type = null,
                Module = Crest.Templates.Services.TemplateComposites.Module,
                DisplayName = entry.Variant.DisplayName ?? entry.Variant.Name,
                Category = entry.Variant.Category ?? "Variants",
                Icon = entry.Variant.Icon ?? "palette",
            });

        return Ok(known.Concat(variants)
            .OrderBy(component => component.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ComponentSchemaModel.From)
            .ToArray());
    }
}

public sealed record ComponentSchemaModel(
    string Name,
    string? DisplayName,
    string? Category,
    string? Icon,
    string? Module,
    string? Permission,
    string Instances,
    bool IsWrapper,
    bool IsHost,
    bool IsOverlay,
    ParameterSchemaModel[] Parameters,
    ZoneSchemaModel[] Zones,
    string Source = "code")
{
    public static ComponentSchemaModel From(ComponentDescriptor component) => new(
        component.Name,
        component.DisplayName,
        component.Category,
        component.Icon,
        component.Module,
        component.Permission,
        component.Instances.ToString(),
        component.IsWrapper,
        component.IsHost,
        component.IsOverlay,
        component.Parameters.Select(parameter => new ParameterSchemaModel(
            parameter.Name,
            parameter.Kind.ToString(),
            parameter.Editor,
            parameter.DisplayName,
            parameter.Group,
            parameter.Required,
            parameter.Hidden,
            parameter.Advanced,
            parameter.ReadOnly,
            parameter.Choices?.ToArray(),
            parameter.Accepts?.ToArray(),
            (parameter.ValueType ?? parameter.ClrType).Name)).ToArray(),
        component.Zones.Select(zone => new ZoneSchemaModel(zone.Name, [.. zone.Allowed], zone.Min, zone.Max)).ToArray(),
        component.Type is null ? "template" : "code");
}

public sealed record ParameterSchemaModel(
    string Name,
    string Kind,
    string Editor,
    string? DisplayName,
    string? Group,
    bool Required,
    bool Hidden,
    bool Advanced,
    bool ReadOnly,
    string[]? Choices,
    string[]? Accepts,
    string Type);

public sealed record ZoneSchemaModel(string Name, string[] Allowed, int Min, int Max);
