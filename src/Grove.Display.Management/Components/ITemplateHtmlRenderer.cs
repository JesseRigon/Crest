#nullable enable

namespace Grove.Display.Management.Components;

/// <summary>
/// Renders a named template (Templates content, or its shipped default) to static HTML with a
/// model: the display tree the template evaluates to, through <see cref="IDisplayHtmlRenderer"/>.
/// What email bodies are made of (step 7: the Razor email shapes are retired). Implemented by the
/// Templates module; absent while it is not enabled, when callers fall back to their own text.
/// </summary>
public interface ITemplateHtmlRenderer
{
    /// <summary>The template's HTML with <paramref name="model"/> as <c>Model</c>; null when no published or shipped template has the name.</summary>
    Task<string?> RenderAsync(string templateName, object model, CancellationToken cancellationToken = default);
}
