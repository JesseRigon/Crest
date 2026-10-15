#nullable enable

using Grove.Access;
using Grove.DisplayManagement.Zones;

namespace Grove.DisplayManagement.Layout;

/// <summary>What a layout contribution sees: the page's layout shape and what the page shows.</summary>
public sealed record LayoutTreeContext(IZoneHolding Layout, object? ContentItem, CallerContext? Caller);

/// <summary>
/// A build step that fills a page's layout before it is mapped to the display tree
/// (docs/blazor-display.md § 6, rework step 6: "Layers as a build step"). A contributor adds
/// shapes to the layout's zones by key; the tree builder maps them with everything else, so a
/// contribution is a node of the one tree, never HTML. Run in <see cref="Order"/>.
/// </summary>
public interface ILayoutTreeContributor
{
    int Order => 0;

    Task ContributeAsync(LayoutTreeContext context, CancellationToken cancellationToken = default);
}
