#nullable enable

using Crest.DisplayManagement.ModelBinding;

namespace Crest.DisplayManagement.Components;

/// <summary>
/// The Blazor display beside <see cref="Implementation.IHtmlDisplay"/> (docs/blazor-display.md
/// › Rework plan, step 2): builds the display tree for a model and a display type, or maps a
/// shape tree the display managers already built. The build side (drivers, placement,
/// alternates, zones) is unchanged; this replaces only the last step, so the result is the
/// tree the renderer, the browser and a headless client consume (decision 10). Permissions are
/// applied here, on the server, for the current caller (§ 10).
/// </summary>
public interface IDisplayTreeBuilder
{
    /// <summary>Maps a built shape tree to a display tree, in one pass.</summary>
    /// <param name="shape">The root shape, as <c>IDisplayManager&lt;T&gt;.BuildDisplayAsync</c> or
    /// <c>IContentItemDisplayManager.BuildDisplayAsync</c> returns it.</param>
    /// <param name="component">The component the root is already bound to (the HTML binding
    /// path); null resolves it through the registry like every other node.</param>
    Task<DisplayTree> BuildAsync(IShape shape, string? component = null, CancellationToken cancellationToken = default);

    /// <summary>Builds the display shape of <paramref name="model"/> through
    /// <see cref="IDisplayManager{TModel}"/>, then maps it.</summary>
    Task<DisplayTree> BuildAsync<TModel>(TModel model, IUpdateModel updater, string displayType = "", string groupId = "", CancellationToken cancellationToken = default);
}
