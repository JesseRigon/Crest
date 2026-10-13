#nullable enable

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Logging;

namespace Crest.DisplayManagement.Components;

/// <summary>
/// The HTML side of a component binding (<see cref="Descriptors.ShapeBinding.ComponentName"/>):
/// where the server still renders shapes as HTML (MVC pages, Liquid), a shape bound to a
/// registered component is mapped to a display node with its zones and rendered in one pass by
/// <see cref="HtmlRenderer"/> and <see cref="DisplayTreeRenderer"/>. Its children are nodes of
/// the same tree, never HTML rendered on their own and fed in.
/// </summary>
public sealed class ComponentShapeRenderer
{
    private readonly IDisplayTreeBuilder _builder;
    private readonly IServiceProvider _services;
    private readonly ILoggerFactory _loggerFactory;

    public ComponentShapeRenderer(IDisplayTreeBuilder builder, IServiceProvider services, ILoggerFactory loggerFactory)
    {
        _builder = builder;
        _services = services;
        _loggerFactory = loggerFactory;
    }

    public async Task<IHtmlContent> RenderAsync(IShape shape, string component)
    {
        var tree = await _builder.BuildAsync(shape, component);
        return new HtmlString(await RenderAsync(tree.Root, _services, _loggerFactory));
    }

    /// <summary>Renders a display node to static HTML with the same renderer the browser runs.</summary>
    public static Task<string> RenderAsync(DisplayNode node, IServiceProvider services, ILoggerFactory loggerFactory) =>
        new DisplayHtmlRenderer(services, loggerFactory).RenderAsync(node);
}
