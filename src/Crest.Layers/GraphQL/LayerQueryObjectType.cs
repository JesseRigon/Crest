using System.Linq.Expressions;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Crest.Apis.GraphQL;
using Crest.ContentManagement;
using Crest.ContentManagement.GraphQL.Queries;
using Crest.ContentManagement.Records;
using Crest.Layers.Models;
using Crest.Layers.Services;
using Crest.Rules;

namespace Crest.Layers.GraphQL;

public class LayerQueryObjectType : ObjectGraphType<Layer>
{
    public LayerQueryObjectType(IStringLocalizer<LayerQueryObjectType> S)
    {
        Name = "Layer";

        Field(layer => layer.Name)
            .Description(S["The name of the layer."]);

        Field<ListGraphType<StringGraphType>, IEnumerable<Condition>>("layerrule")
            .Description(S["The rule that activates the layer."])
            .Resolve(ctx => ctx.Source.LayerRule.Conditions);

        Field(layer => layer.Description)
            .Description(S["The description of the layer."]);

        Field<ListGraphType<LayerWidgetQueryObjectType>, IEnumerable<ContentItem>>("widgets")
            .Description(S["The widgets for this layer."])
            .Argument<PublicationStatusGraphType>("status", S["publication status of the widgets"])
            .ResolveLockedAsync(GetWidgetsForLayerAsync);

        async ValueTask<IEnumerable<ContentItem>> GetWidgetsForLayerAsync(IResolveFieldContext<Layer> context)
        {
            var layerService = context.RequestServices.GetService<ILayerService>();

            var filter = GetVersionFilter(context.GetArgument<PublicationStatusEnum>("status"));
            var widgets = await layerService.GetLayerWidgetsAsync(filter);

            var layerWidgets = widgets?.Where(item =>
            {
                if (!item.TryGet<LayerMetadata>(out var metadata))
                {
                    return false;
                }

                return metadata.Layer == context.Source.Name;
            });

            return layerWidgets;
        }
    }

    private static Expression<Func<ContentItemIndex, bool>> GetVersionFilter(PublicationStatusEnum status)
        => status switch
        {
            PublicationStatusEnum.Published => x => x.Published,
            PublicationStatusEnum.Draft => x => x.Latest && !x.Published,
            PublicationStatusEnum.Latest => x => x.Latest,
            PublicationStatusEnum.All => x => true,
            _ => x => x.Published,
        };
}
