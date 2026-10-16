using System.Text.Json.Nodes;
using Grove.Display.Management.Components;
using Grove.Infrastructure.Html;
using Grove.Display.Templates.Models;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Contents.Management;

namespace Grove.Display.Templates.Services;

/// <summary>
/// Binds the <c>DisplayTreeFieldSet</c> shape (docs/widgets.md, ruling 2026-10-13) to the item's
/// stored tree, prepared for this build: node ids scoped under the part's instance id (and every
/// reference to them with them), markup leaves sanitized, a node naming a published template
/// expanded as a composite with the same limits a template runs under, and the node budget
/// counted. The tree builder then fills, filters and overrides it like a template's tree.
/// </summary>
public sealed class DisplayTreeFieldSetBinding(
    TemplateTreeEvaluator evaluator,
    IComponentRegistry registry,
    IOptions<DisplayTreeOptions> options,
    IServiceProvider services) : IDisplayTemplateBinding
{
    public async Task<DisplayNode?> BindAsync(DisplayTemplateRequest request, CancellationToken cancellationToken = default)
    {
        // DisplayTreeFieldSet's shape (DisplayTree), and its display types' shapes (DisplayTree_Summary).
        var shapeType = ContentFieldSetName.Of<DisplayTreeFieldSet>();
        if (request.ShapeType != shapeType && !request.ShapeType.StartsWith(shapeType + "_", StringComparison.Ordinal))
        {
            return null;
        }

        request.Shape.Properties.TryGetValue("Tree", out var stored);
        if (stored is null)
        {
            return DisplayNode.Markup(request.InstanceId, string.Empty);
        }

        if (!DisplayTreeJson.TryParse(stored as JsonNode, out var root, out var error))
        {
            return DisplayNode.Error(request.InstanceId, new(request.InstanceId, "parse", error ?? "The page body does not parse."));
        }

        var budget = new TemplateBudget(options.Value, cancellationToken);
        var state = new TemplateTreeState(request.InstanceId, registry, services.GetService<IHtmlSanitizerService>(), budget, services, evaluator);
        try
        {
            var prepared = await PrepareAsync(root!, request.InstanceId, state);
            if (state.Diagnostics.Count > 0)
            {
                (prepared.Diagnostics ??= []).AddRange(state.Diagnostics);
            }

            return prepared;
        }
        catch (TemplateLimitException exception)
        {
            return DisplayNode.Error(request.InstanceId, new(request.InstanceId, "limit", exception.Message));
        }
    }

    private async Task<DisplayNode> PrepareAsync(DisplayNode stored, string instanceId, TemplateTreeState state)
    {
        var ids = DisplayTreeValidator.Walk(stored).Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        string Scope(string id) => ids.Contains(id) ? instanceId + "." + id : id;

        var sanitizer = services.GetService<IHtmlSanitizerService>();
        async Task<DisplayNode> CopyAsync(DisplayNode node)
        {
            state.Budget.Count();
            var id = Scope(node.Id);

            // A published template placed by name: evaluated as a composite with the node's
            // properties as its arguments, under this build's budget.
            if (!registry.TryGet(node.Component, out _) && node.Component is not (DisplayNode.MarkupComponent or DisplayNode.ErrorComponent)
                && await evaluator.EvaluateCompositeAsync(node.Component, id, [.. node.Properties.Select(pair => (pair.Key, TemplateTreeTags.ToFluid(pair.Value)))], state) is { } composite)
            {
                return composite;
            }

            var copy = new DisplayNode
            {
                Id = id,
                Component = node.Component,
                Module = node.Module ?? (registry.TryGet(node.Component, out var descriptor) ? descriptor.Module : null),
                Properties = new(node.Properties, StringComparer.OrdinalIgnoreCase),
                Bindings = node.Bindings?.ToDictionary(pair => pair.Key, pair => DisplayPredicates.MapNodes(pair.Value, Scope), StringComparer.OrdinalIgnoreCase),
                Slots = node.Slots?.ToDictionary(pair => pair.Key, pair => new DisplaySlot
                {
                    Query = pair.Value.Query,
                    Path = pair.Value.Path,
                    Parameters = pair.Value.Parameters?.ToDictionary(parameter => parameter.Key, parameter => DisplayPredicates.MapNodes(parameter.Value, Scope), StringComparer.Ordinal),
                }, StringComparer.OrdinalIgnoreCase),
                OutputAliases = node.OutputAliases?.ToDictionary(pair => pair.Key, pair => DisplayPredicates.MapNodes(pair.Value, Scope), StringComparer.OrdinalIgnoreCase),
                Events = node.Events?.ToDictionary(pair => pair.Key, pair => pair.Value.Select(action => new DisplayAction
                {
                    Kind = action.Kind,
                    Target = action.Target is { } target && action.Kind is DisplayActionKind.Open or DisplayActionKind.Close or DisplayActionKind.Toggle ? Scope(target) : action.Target,
                    Value = action.Value is { } value ? DisplayPredicates.MapNodes(value, Scope) : null,
                    Workflow = action.Workflow,
                    Arguments = action.Arguments?.ToDictionary(argument => argument.Key, argument => DisplayPredicates.MapNodes(argument.Value, Scope), StringComparer.Ordinal),
                }).ToList(), StringComparer.OrdinalIgnoreCase),
                Diagnostics = node.Diagnostics is { Count: > 0 } ? [.. node.Diagnostics] : null,
            };

            // Stored markup is the author's text: sanitized here, as a template's text is when it is evaluated.
            if (string.Equals(copy.Component, DisplayNode.MarkupComponent, StringComparison.OrdinalIgnoreCase))
            {
                var html = copy.Properties.TryGetValue(DisplayNode.MarkupProperty, out var raw) && raw is JsonValue text && text.TryGetValue<string>(out var value) ? value : string.Empty;
                copy.Properties[DisplayNode.MarkupProperty] = sanitizer?.Sanitize(html) ?? string.Empty;
            }

            foreach (var (zone, children) in node.Zones)
            {
                var copied = new List<DisplayNode>(children.Count);
                foreach (var child in children)
                {
                    copied.Add(await CopyAsync(child));
                }

                copy.Zones[zone] = copied;
            }

            return copy;
        }

        return await CopyAsync(stored);
    }
}
