#nullable enable

using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Infrastructure.Html;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Display.Management.Components;

/// <summary>
/// Decides whether the viewer may run one server action of a display tree (§ 4a, § 10). Modules
/// that own an action kind register one: the workflows module answers for workflow actions
/// with the same decision its run endpoint takes. A filter that does not know the action
/// answers null and leaves it to the others.
/// </summary>
public interface IDisplayActionFilter
{
    Task<bool?> AllowsAsync(CallerContext caller, DisplayAction action, CancellationToken cancellationToken = default);
}

/// <summary>
/// The node filter of § 10 applied to a built tree: a node is left out when its component
/// requires a permission the viewer lacks, or when one of its events runs a server action the
/// viewer may not run (a context menu item for a workflow the viewer cannot run). The tree
/// that leaves the server holds only what the viewer may see and do. Bindings and actions that
/// pointed at a dropped node are removed with it, so the tree stays valid.
/// </summary>
public interface IDisplayTreeFilter
{
    Task<DisplayNode?> FilterAsync(DisplayNode root, CallerContext? caller, CancellationToken cancellationToken = default);
}

public sealed class DisplayTreeFilter(IComponentRegistry registry, IServiceProvider services) : IDisplayTreeFilter
{
    public async Task<DisplayNode?> FilterAsync(DisplayNode root, CallerContext? caller, CancellationToken cancellationToken = default)
    {
        var decisions = new Dictionary<string, bool>(StringComparer.Ordinal);
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        var kept = await FilterNodeAsync(root, caller, decisions, dropped, cancellationToken);

        if (kept is not null && dropped.Count > 0)
        {
            foreach (var node in DisplayTreeValidator.Walk(kept))
            {
                Unlink(node, dropped);
            }
        }

        return kept;
    }

    private async Task<DisplayNode?> FilterNodeAsync(DisplayNode node, CallerContext? caller, Dictionary<string, bool> decisions, HashSet<string> dropped, CancellationToken cancellationToken)
    {
        if (registry.TryGet(node.Component, out var descriptor) && !string.IsNullOrEmpty(descriptor.Permission)
            && !await DecideAsync(caller, "permission:" + descriptor.Permission, decisions, () => PermissionAsync(caller, descriptor.Permission, cancellationToken)))
        {
            Drop(node, dropped);
            return null;
        }

        if (descriptor is not null)
        {
            SanitizeMarkup(node, descriptor);
        }

        foreach (var action in (node.Events?.Values ?? Enumerable.Empty<List<DisplayAction>>()).SelectMany(list => list).Where(action => !action.IsClientOnly))
        {
            var key = $"action:{action.Kind}:{action.Workflow}";
            if (!await DecideAsync(caller, key, decisions, () => ActionAsync(caller, action, cancellationToken)))
            {
                Drop(node, dropped);
                return null;
            }
        }

        foreach (var (zone, children) in node.Zones.ToArray())
        {
            var keptChildren = new List<DisplayNode>(children.Count);
            foreach (var child in children)
            {
                if (await FilterNodeAsync(child, caller, decisions, dropped, cancellationToken) is { } keptChild)
                {
                    keptChildren.Add(keptChild);
                }
            }

            node.Zones[zone] = keptChildren;
        }

        return node;
    }

    private void SanitizeMarkup(DisplayNode node, ComponentDescriptor descriptor)
    {
        foreach (var parameter in descriptor.Parameters)
        {
            if (parameter.Editor != ComponentSchema.Editors.Markup || !node.Properties.TryGetValue(parameter.Name, out var value) || value is null)
            {
                continue;
            }

            var html = value is JsonValue text && text.TryGetValue<string>(out var raw) ? raw : value.ToJsonString();
            var sanitizer = services.GetService<IHtmlSanitizerService>();
            node.Properties[parameter.Name] = sanitizer is null ? string.Empty : sanitizer.Sanitize(html);
        }
    }

    private static async Task<bool> DecideAsync(CallerContext? caller, string key, Dictionary<string, bool> decisions, Func<Task<bool>> decide)
    {
        if (caller is null)
        {
            return false;
        }

        if (!decisions.TryGetValue(key, out var allowed))
        {
            decisions[key] = allowed = await decide();
        }

        return allowed;
    }

    private async Task<bool> PermissionAsync(CallerContext? caller, string permission, CancellationToken cancellationToken) =>
        caller is not null && services.GetService<IAccessDecision>() is { } decision
        && (await decision.DecideAsync(caller, permission, cancellationToken: cancellationToken)).IsAllowed;

    // A server action nobody vouches for is not shown: navigate and submit are allowed (the
    // target page and the submit endpoint check the request themselves), a workflow needs a
    // filter that knows it.
    private async Task<bool> ActionAsync(CallerContext? caller, DisplayAction action, CancellationToken cancellationToken)
    {
        if (caller is null)
        {
            return false;
        }

        foreach (var filter in services.GetServices<IDisplayActionFilter>())
        {
            if (await filter.AllowsAsync(caller, action, cancellationToken) is { } answer)
            {
                return answer;
            }
        }

        return action.Kind is DisplayActionKind.Navigate or DisplayActionKind.Submit;
    }

    private static void Drop(DisplayNode node, HashSet<string> dropped)
    {
        foreach (var each in DisplayTreeValidator.Walk(node))
        {
            dropped.Add(each.Id);
        }
    }

    private static void Unlink(DisplayNode node, HashSet<string> dropped)
    {
        if (node.Bindings is { } bindings)
        {
            foreach (var name in bindings.Where(pair => DisplayPredicates.Sources(pair.Value).Any(dropped.Contains)).Select(pair => pair.Key).ToArray())
            {
                bindings.Remove(name);
            }
        }

        if (node.Events is { } events)
        {
            foreach (var list in events.Values)
            {
                list.RemoveAll(action => action.Target is { } target && dropped.Contains(target) && action.Kind is DisplayActionKind.Open or DisplayActionKind.Close or DisplayActionKind.Toggle);
            }
        }
    }
}

/// <summary>
/// Checks a server action of a tree that is being saved (a template), as the module that owns the
/// action knows it: the workflows module answers whether the workflow offers a UI action contract
/// and whether the action's arguments match it. Returns the problems; none when it does not know
/// the action.
/// </summary>
public interface IDisplayActionValidator
{
    Task<IReadOnlyList<string>> ValidateAsync(DisplayAction action, CancellationToken cancellationToken = default);
}
