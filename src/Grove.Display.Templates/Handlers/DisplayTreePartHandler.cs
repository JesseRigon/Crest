using Grove.Contents.Management.Handlers;
using Grove.Display.Management.Components;
using Grove.Display.Templates.Models;
using Grove.Display.Templates.Services;

namespace Grove.Display.Templates.Handlers;

/// <summary>
/// Validates an item's page body when the item is saved (docs/widgets.md: components must be
/// registered, properties must fit the schema, no scripting): the stored check of
/// <see cref="DisplayTreeValidator"/> against the site shell's registry (the body renders on the
/// site), a published composite counting as a component, and each workflow action checked by
/// the module that owns it.
/// </summary>
public sealed class DisplayTreePartHandler(
    IComponentRegistryManager registries,
    ITemplateCatalog templates,
    IEnumerable<IDisplayActionValidator> actionValidators) : ContentPartHandler<DisplayTreePart>
{
    public override async Task ValidatingAsync(ValidateContentContext context, DisplayTreePart part)
    {
        if (part.Tree is null)
        {
            return;
        }

        if (!DisplayTreeJson.TryParse(part.Tree, out var root, out var error))
        {
            context.Fail(error ?? "The page body does not parse.", nameof(DisplayTreePart));
            return;
        }

        var composites = (await templates.ListPublishedAsync()).Select(template => template.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var registry = registries.GetRegistry(ShellKind.Site);
        foreach (var diagnostic in DisplayTreeValidator.ValidateStored(root!, registry))
        {
            // A composite is placed by name like a component; the binding expands it.
            if (diagnostic.Action == "unknown" && DisplayTreeValidator.Walk(root!).Any(node => node.Id == diagnostic.Path && composites.Contains(node.Component)))
            {
                continue;
            }

            context.Fail($"{diagnostic.Path}: {diagnostic.Reason}", nameof(DisplayTreePart));
        }

        foreach (var node in DisplayTreeValidator.Walk(root!))
        {
            foreach (var (eventName, actions) in node.Events ?? [])
            {
                foreach (var action in actions.Where(action => !action.IsClientOnly))
                {
                    foreach (var validator in actionValidators)
                    {
                        foreach (var problem in await validator.ValidateAsync(action))
                        {
                            context.Fail($"{node.Id}.{eventName}: {problem}", nameof(DisplayTreePart));
                        }
                    }
                }
            }
        }
    }

    // The body aspect (the full-text aspect, feeds) is the body's text.
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context, DisplayTreePart part) =>
        context.ForAsync<Grove.Contents.Management.Models.BodyAspect>(async aspect =>
        {
            if (part.Tree is null)
            {
                return;
            }

            var composites = (await templates.ListPublishedAsync())
                .Where(template => template.IsComposite)
                .GroupBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Source, StringComparer.OrdinalIgnoreCase);
            aspect.Body = new Microsoft.AspNetCore.Html.HtmlString(System.Net.WebUtility.HtmlEncode(Services.DisplayTreeText.Extract(part.Tree, composites)));
        });
}
