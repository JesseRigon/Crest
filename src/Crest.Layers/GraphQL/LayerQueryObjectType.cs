using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Crest.Layers.Models;
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
    }
}
