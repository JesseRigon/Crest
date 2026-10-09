using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.Flows.Models;

namespace Crest.Flows.GraphQL;

public class FlowPartQueryObjectType : ObjectGraphType<FlowPart>
{
    public FlowPartQueryObjectType(IStringLocalizer<FlowPartQueryObjectType> S)
    {
        Name = "FlowPart";
        Description = S["A FlowPart allows to add content items directly within another content item"];

        Field<ListGraphType<ContentItemInterface>>("widgets")
           .Description(S["The widgets."])
           .Resolve(context => context.Source.Widgets);
    }
}
