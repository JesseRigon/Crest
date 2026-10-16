using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Display.Management.Components;
using Grove.Display.Templates.Models;

namespace Grove.Display.Templates.Services;

/// <summary>
/// What the page body's system flow does (<see cref="Workflows.DisplayTreeWorkflowProvider"/>): an
/// item's page body is checked when the item is validated (docs/widgets.md: components must be
/// registered, properties must fit the schema, no scripting), by the stored check of
/// <see cref="DisplayTreeValidator"/> against the site shell's registry (the body renders on the
/// site), a published composite counting as a component, and each workflow action checked by the
/// module that owns it.
/// </summary>
public sealed class DisplayTreeBehaviour(
    IComponentRegistryManager registries,
    ITemplateCatalog templates,
    IEnumerable<IDisplayActionValidator> actionValidators)
{
    /// <summary>The refusal of a page body that does not parse or holds what the site may not render; null when it is fine.</summary>
    public async Task<ContentFieldRefusal?> RefusalAsync(ContentItem item)
    {
        if (!item.TryGet<DisplayTreeFieldSet>(out var part) || part.Tree is null)
        {
            return null;
        }

        var problems = await ProblemsAsync(part);
        return problems.Count == 0 ? null : new ContentFieldRefusal(string.Join(" ", problems), ContentFieldSetName.Of<DisplayTreeFieldSet>());
    }

    /// <summary>Every problem of the page body, each prefixed with the node it is about.</summary>
    public async Task<IReadOnlyList<string>> ProblemsAsync(DisplayTreeFieldSet part)
    {
        if (!DisplayTreeJson.TryParse(part.Tree, out var root, out var error))
        {
            return [error ?? "The page body does not parse."];
        }

        var problems = new List<string>();
        var composites = (await templates.ListPublishedAsync()).Select(template => template.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var registry = registries.GetRegistry(ShellKind.Site);
        foreach (var diagnostic in DisplayTreeValidator.ValidateStored(root!, registry))
        {
            // A composite is placed by name like a component; the binding expands it.
            if (diagnostic.Action == "unknown" && DisplayTreeValidator.Walk(root!).Any(node => node.Id == diagnostic.Path && composites.Contains(node.Component)))
            {
                continue;
            }

            problems.Add($"{diagnostic.Path}: {diagnostic.Reason}");
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
                            problems.Add($"{node.Id}.{eventName}: {problem}");
                        }
                    }
                }
            }
        }

        return problems;
    }
}
