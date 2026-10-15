using System.Text.Json.Nodes;
using Grove.Display.Management.Components;
using Grove.Documents;
using Grove.Display.Templates.Models;

namespace Grove.Display.Templates.Services;

/// <summary>A variant resolved down its chain: the component (or composite) it places and the values it sets, base first.</summary>
public sealed record ResolvedVariant(string Variant, string Component, JsonObject Values, IReadOnlyList<string> Chain);

/// <summary>
/// The overrides of § 5a: resolution of a variant down to what it places (each link's values over
/// its base's: base node → node overrides → instance → instance overrides), and the instance
/// overrides applied to a built tree. An override never touches a bound or slotted value, an
/// event, or a parameter the component does not have.
/// </summary>
public sealed class ComponentOverrides(IDocumentManager<ComponentOverridesDocument> documents, IComponentRegistry registry) : IDisplayTreeOverrides
{
    public const int MaxChain = 16;

    public Task<ComponentOverridesDocument> LoadAsync() => documents.GetOrCreateMutableAsync();

    public Task<ComponentOverridesDocument> GetAsync() => documents.GetOrCreateImmutableAsync();

    public Task SaveAsync(ComponentOverridesDocument document) => documents.UpdateAsync(document);

    /// <summary>The variant named <paramref name="name"/> resolved to its component; null when there is none or its chain is broken.</summary>
    public async Task<ResolvedVariant?> ResolveVariantAsync(string name)
    {
        var document = await GetAsync();
        return Resolve(document, name, out _);
    }

    public static ResolvedVariant? Resolve(ComponentOverridesDocument document, string name, out string? problem)
    {
        problem = null;
        var chain = new List<ComponentVariant>();
        var current = name;
        while (document.Variants.FirstOrDefault(variant => string.Equals(variant.Name, current, StringComparison.OrdinalIgnoreCase)) is { } variant)
        {
            if (chain.Contains(variant) || chain.Count >= MaxChain)
            {
                problem = $"The variant '{name}' inherits from itself.";
                return null;
            }

            chain.Add(variant);
            current = variant.Base;
        }

        if (chain.Count == 0)
        {
            return null;
        }

        // Base first: each variant's own values over what it inherits.
        var values = new JsonObject();
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            foreach (var (key, value) in chain[i].Values)
            {
                values[key] = value?.DeepClone();
            }
        }

        return new ResolvedVariant(chain[0].Name, current, values, [.. chain.Select(variant => variant.Name)]);
    }

    /// <summary>The problems with <paramref name="values"/> as overrides of <paramref name="component"/>: unknown parameters, events, wrong types.</summary>
    public static IReadOnlyList<string> Check(ComponentDescriptor component, JsonObject values)
    {
        var errors = new List<string>();
        foreach (var (name, _) in values)
        {
            if (component.Parameter(name) is { IsEvent: true })
            {
                errors.Add($"'{name}' is an event of '{component.Name}': events are wired, never overridden.");
            }
        }

        ComponentParameters.Bind(component, values.Where(pair => component.Parameter(pair.Key) is not { IsEvent: true }).Select(pair => KeyValuePair.Create(pair.Key, (object?)pair.Value?.DeepClone())), errors);
        return errors.Where(error => !error.Contains("is required", StringComparison.Ordinal)).ToArray();
    }

    /// <summary>Applies the instance overrides to <paramref name="root"/>'s nodes: literal values only, never over a binding or a slot.</summary>
    public async Task ApplyAsync(DisplayNode root, CancellationToken cancellationToken = default)
    {
        var document = await GetAsync();
        if (document.Instances.Count == 0)
        {
            return;
        }

        var byId = document.Instances.ToDictionary(instance => instance.InstanceId, StringComparer.Ordinal);
        foreach (var node in DisplayTreeValidator.Walk(root))
        {
            if (!byId.TryGetValue(node.Id, out var instance) || !registry.TryGet(node.Component, out var descriptor))
            {
                continue;
            }

            foreach (var (name, value) in instance.Values)
            {
                if (descriptor.Parameter(name) is not { IsEvent: false } parameter
                    || node.Bindings?.ContainsKey(parameter.Name) == true
                    || node.Slots?.ContainsKey(parameter.Name) == true
                    || !ComponentParameters.TryConvert(value?.DeepClone(), parameter.ClrType, out var converted, out _))
                {
                    continue;
                }

                node.Properties[parameter.Name] = DisplayState.ToJson(converted);
            }
        }
    }
}

/// <summary>
/// The templates' part of a tree's composite version (decision 12): the published template set
/// and the overrides document. A template is content, so its publish also bumps the content
/// version; the overrides are a document of their own.
/// </summary>
public sealed class TemplatesTreeVersion(ITemplateCatalog templates, ComponentOverrides overrides) : IDisplayTreeVersionPart
{
    public async ValueTask<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var published = await templates.ListPublishedAsync();
        var document = await overrides.GetAsync();
        return string.Join(',', published.Select(template => template.ContentItemVersionId).Order(StringComparer.Ordinal)) + "|" + document.Identifier;
    }
}
