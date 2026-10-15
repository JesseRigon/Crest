#nullable enable

using Grove.Contents.Management;
using Grove.Display.Management.Components;

namespace Grove.Display;

/// <summary>
/// Builds a content item's editor as a display tree (docs/blazor-display.md § 7): a
/// <c>ContentEditor</c> holding the item and its definition, a section per part, each field's and
/// part's editor component. The page that renders the tree saves what its <c>ContentEditor</c>
/// publishes. For editors of content kept elsewhere than in the content store (custom site
/// settings, custom user settings) as for content items.
/// </summary>
public interface IContentEditorTreeBuilder
{
    /// <summary>The editor tree of <paramref name="item"/> (an existing item, or a new one of its type); null when its type has no definition.</summary>
    Task<DisplayTree?> BuildAsync(ContentItem item, CancellationToken cancellationToken = default);
}
