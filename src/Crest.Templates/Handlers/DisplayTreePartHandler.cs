using Crest.ContentManagement.Handlers;
using Crest.DisplayManagement.Components;
using Crest.Templates.Models;
using Crest.Templates.Services;

namespace Crest.Templates.Handlers;

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
}
