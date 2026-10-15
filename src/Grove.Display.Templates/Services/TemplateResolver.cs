using Grove.Display.Management.Components;
using Grove.Display.Management.Theming;

namespace Grove.Display.Templates.Services;

/// <summary>
/// Templates a module ships (decision 7: the chain ends at "the module's shipped default"): the
/// level below the tenant's own templates. A provider may answer per request (the default
/// <c>Content</c> template exists only where the tenant turned it on, decision 10).
/// </summary>
public interface IShippedTemplateProvider
{
    Task<IReadOnlyList<TemplateRecord>> GetTemplatesAsync();
}

/// <summary>
/// The template hierarchy (decision 7): a shape's names, most specific first, each tried with
/// the qualifiers dropped one at a time (theme and display type, theme, display type, none),
/// first among the tenant's templates and then among the shipped ones.
/// Theme is the one placement qualifier: the current request's theme is its shell's. Only
/// published templates resolve; a draft is reached through preview only.
/// </summary>
public sealed class TemplateResolver(ITemplateCatalog store, IThemeManager themes, IEnumerable<IShippedTemplateProvider>? shipped = null)
{
    public async Task<TemplateRecord?> ResolveAsync(IReadOnlyList<string> names, string? displayType)
    {
        var published = await store.ListPublishedAsync();
        var defaults = new List<TemplateRecord>();
        foreach (var provider in shipped ?? [])
        {
            defaults.AddRange(await provider.GetTemplatesAsync());
        }

        if (published.Count == 0 && defaults.Count == 0)
        {
            return null;
        }

        var theme = (await themes.GetThemeAsync())?.Id;
        foreach (var name in names)
        {
            if ((Match(published, name, theme, displayType) ?? Match(defaults, name, theme, displayType)) is { } match)
            {
                return match;
            }
        }

        return null;
    }

    private static TemplateRecord? Match(IReadOnlyList<TemplateRecord> templates, string name, string? theme, string? displayType)
    {
        var candidates = templates.Where(template => string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        foreach (var (byTheme, byDisplayType) in new[] { (true, true), (true, false), (false, true), (false, false) })
        {
            var match = candidates.FirstOrDefault(template =>
                (byTheme ? Same(template.Theme, theme) : string.IsNullOrEmpty(template.Theme))
                && (byDisplayType ? Same(template.DisplayType, displayType) : string.IsNullOrEmpty(template.DisplayType)));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>The names a shape resolves by: its alternates, last (most specific) first, then its type's segments.</summary>
    public static IReadOnlyList<string> NamesOf(string type, IReadOnlyList<string> alternates)
    {
        var names = new List<string>();
        for (var i = alternates.Count - 1; i >= 0; i--)
        {
            names.Add(alternates[i]);
        }

        for (var name = type; name.Length > 0; name = name.LastIndexOf("__", StringComparison.Ordinal) is var cut and > 0 ? name[..cut] : string.Empty)
        {
            names.Add(name);
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool Same(string? qualifier, string? current) =>
        !string.IsNullOrEmpty(qualifier) && string.Equals(qualifier, current, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A draft under preview (§ 11, preview is evaluation): the Templates page sends a template's
/// source with a content item; for that request the source binds its name ahead of every
/// published template. Scoped to the request; never cached.
/// </summary>
public sealed class TemplatePreview
{
    public string? Name { get; set; }

    public string? Source { get; set; }
}

/// <summary>Binds published templates (and a previewed draft) to shapes for the tree builder (§ 2).</summary>
public sealed class TemplateDisplayBinding(TemplateResolver resolver, TemplateTreeEvaluator evaluator, TemplatePreview preview) : IDisplayTemplateBinding
{
    public async Task<DisplayNode?> BindAsync(DisplayTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var names = TemplateResolver.NamesOf(request.ShapeType, request.Alternates);

        string? source = null;
        string? name = null;
        if (preview.Name is { Length: > 0 } previewName && preview.Source is not null && names.Contains(previewName, StringComparer.OrdinalIgnoreCase))
        {
            (source, name) = (preview.Source, previewName);
        }
        else if (await resolver.ResolveAsync(names, request.DisplayType) is { } template)
        {
            (source, name) = (template.Source, template.Name);
        }

        if (source is null)
        {
            return null;
        }

        var evaluation = await evaluator.EvaluateAsync(source, request.Shape, request.InstanceId, name, cancellationToken: cancellationToken);
        if (evaluation.Diagnostics.Count > 0)
        {
            (evaluation.Root.Diagnostics ??= []).AddRange(evaluation.Diagnostics.Where(diagnostic => !evaluation.Root.Diagnostics.Contains(diagnostic)));
        }

        return evaluation.Root;
    }
}
