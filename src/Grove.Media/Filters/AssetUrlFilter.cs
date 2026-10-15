using System.Text.Json.Nodes;
using Fluid;
using Fluid.Values;
using Grove.Liquid;
using Grove.Media.Fields;
using Grove.Media.Library;

namespace Grove.Media.Filters;

/// <summary>
/// <c>asset_url</c> (ruling F1): the <c>/files/{placementId}</c> URL of what it is given, through the
/// access check, signed when the file is private. It takes a placement id, a <c>/files/...</c> or
/// <c>/media/...</c> URL, a media library path (templates written before ids), or a media field
/// itself, whose first item it takes, id first and path second:
/// <code>{{ Model.ContentItem.Content.Blog.Image | asset_url }}</code>
/// A query string is kept, so <c>resize_url</c> can follow. What the viewer may not see keeps its
/// <c>/media</c> form, which answers 404.
/// </summary>
public class AssetUrlFilter : ILiquidFilter
{
    private readonly IMediaReferenceResolver _references;

    public AssetUrlFilter(IMediaReferenceResolver references)
    {
        _references = references;
    }

    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context)
    {
        if (FieldReference(input.ToObjectValue()) is { } reference)
        {
            var resolved = await _references.ResolveAsync(reference);
            return StringValue.Create(resolved?.Url ?? string.Empty);
        }

        return StringValue.Create(await _references.GetUrlAsync(input.ToStringValue()));
    }

    /// <summary>The first item of a media field, given as the field or as its stored JSON.</summary>
    internal static MediaReference? FieldReference(object value) => value switch
    {
        MediaField field => field.FirstReference(),
        JsonObject json => MediaFieldReferences.FromJson(json)?.FirstReference(),
        System.Text.Json.Dynamic.JsonDynamicObject dynamicJson => MediaFieldReferences.FromJson((JsonObject)dynamicJson)?.FirstReference(),
        _ => null,
    };
}
