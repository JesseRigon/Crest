using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.Theming;

namespace Crest.Templates.Services;

/// <summary>
/// The template hierarchy (decision 7): a shape's names, most specific first, each tried with
/// the qualifiers dropped one at a time (theme and display type, theme, display type, none).
/// Theme is the one placement qualifier: the current request's theme is its shell's. Only
/// published templates resolve; a draft is reached through preview only.
/// </summary>
public sealed class TemplateResolver(ITemplateCatalog store, IThemeManager themes)
{
    public async Task<TemplateRecord?> ResolveAsync(IReadOnlyList<string> names, string? displayType)
    {
        var published = await store.ListPublishedAsync();
        if (published.Count == 0)
        {
            return null;
        }

        var theme = (await themes.GetThemeAsync())?.Id;
        foreach (var name in names)
        {
            var candidates = published.Where(template => string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (candidates.Length == 0)
            {
                continue;
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
