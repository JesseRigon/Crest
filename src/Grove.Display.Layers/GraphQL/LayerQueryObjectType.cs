using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Display.Layers.Models;
using Grove.Display.Rules;

namespace Grove.Display.Layers.GraphQL;

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
