using Grove.Api;
using Grove.Display.Liquid;
using Grove.Shortcodes.Models;
using Grove.Shortcodes.Services;
using Grove.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Shortcodes;

namespace Grove.Shortcodes.Api;

/// <summary>
/// <c>api/grove/shortcodes/templates</c>: the tenant's shortcode templates (the
/// Grove.Shortcodes.Templates feature), with <c>ManageShortcodeTemplates</c>. A template's name
/// is its key: renaming one replaces it.
/// </summary>
public sealed class ShortcodeTemplatesApi : ApiContributor
{
    public override string Name => "ShortcodeTemplates";

    public override string FeatureId => "Grove.Shortcodes.Templates";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("shortcodes/templates");
        routes.MapGet("", ApiHandlers.Handle<ShortcodeTemplatesHandlers>(h => h.ListAsync()));
        routes.MapPost("", (HttpContext http, [FromBody] ShortcodeTemplateModel request) => ApiHandlers.For<ShortcodeTemplatesHandlers>(http).SaveAsync(null, request));
        routes.MapPut("{name}", (HttpContext http, [FromRoute] string name, [FromBody] ShortcodeTemplateModel request) => ApiHandlers.For<ShortcodeTemplatesHandlers>(http).SaveAsync(name, request));
        routes.MapDelete("{name}", (HttpContext http, [FromRoute] string name) => ApiHandlers.For<ShortcodeTemplatesHandlers>(http).DeleteAsync(name));
    }
}

public sealed class ShortcodeTemplatesHandlers(
    IAuthorizationService authorization,
    ShortcodeTemplatesManager templates,
    IShortcodeDescriptorManager descriptors,
    ILiquidTemplateManager liquid) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (!await CanManageAsync()) return Forbid();

        var document = await templates.GetShortcodeTemplatesDocumentAsync();
        var categories = (await descriptors.GetShortcodeDescriptors())
            .SelectMany(descriptor => descriptor.Categories ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new ShortcodeTemplatesModel(
            [.. document.ShortcodeTemplates.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).Select(entry => ToModel(entry.Key, entry.Value))],
            categories));
    }

    /// <summary>Creates (<paramref name="sourceName"/> null) or replaces a template; a new name renames it.</summary>
    public async Task<IResult> SaveAsync(string? sourceName, ShortcodeTemplateModel request)
    {
        if (!await CanManageAsync()) return Forbid();

        var document = await templates.LoadShortcodeTemplatesDocumentAsync();
        if (sourceName is not null && !document.ShortcodeTemplates.ContainsKey(sourceName))
        {
            return NotFound();
        }

        var name = request.Name?.Trim() ?? string.Empty;
        var errors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("name", "The name is mandatory.");
        }
        else if (!IsValidShortcodeName(name))
        {
            errors.Add("name", "The name contains invalid characters.");
        }
        else if (!string.Equals(name, sourceName, StringComparison.OrdinalIgnoreCase) && document.ShortcodeTemplates.ContainsKey(name))
        {
            errors.Add("name", "A template with the same name already exists.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            errors.Add("content", "The template content is mandatory.");
        }
        else if (!liquid.Validate(request.Content, out var liquidErrors))
        {
            errors.Add("content", $"The template doesn't contain a valid Liquid expression. Details: {string.Join(" ", liquidErrors)}");
        }

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        if (sourceName is not null && !string.Equals(sourceName, name, StringComparison.OrdinalIgnoreCase))
        {
            await templates.RemoveShortcodeTemplateAsync(sourceName);
        }

        var template = new ShortcodeTemplate
        {
            Content = request.Content,
            Hint = request.Hint,
            Usage = request.Usage,
            DefaultValue = request.DefaultValue,
            Categories = [.. (request.Categories ?? []).Where(category => !string.IsNullOrWhiteSpace(category)).Select(category => category.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)],
        };

        await templates.UpdateShortcodeTemplateAsync(name, template);
        return Ok(ToModel(name.ToLowerInvariant(), template));
    }

    public async Task<IResult> DeleteAsync(string name)
    {
        if (!await CanManageAsync()) return Forbid();

        var document = await templates.LoadShortcodeTemplatesDocumentAsync();
        if (!document.ShortcodeTemplates.ContainsKey(name))
        {
            return NotFound();
        }

        await templates.RemoveShortcodeTemplateAsync(name);
        return NoContent();
    }

    private static ShortcodeTemplateModel ToModel(string name, ShortcodeTemplate template) => new()
    {
        Name = name,
        Content = template.Content ?? string.Empty,
        Hint = template.Hint,
        Usage = template.Usage,
        DefaultValue = template.DefaultValue,
        Categories = template.Categories ?? [],
    };

    // The name is a shortcode identifier: it parses as one shortcode of that name.
    private static bool IsValidShortcodeName(string name)
    {
        try
        {
            var nodes = new ShortcodesParser().Parse($"[{name}]");
            return nodes.Count == 1 && nodes[0] is Shortcode shortcode && shortcode.Identifier.Equals(name, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private Task<bool> CanManageAsync() => authorization.AuthorizeAsync(User, ShortcodesPermissions.ManageShortcodeTemplates);
}
