using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Routes.Auto.Models;

namespace Grove.Routes.Auto.GraphQL;

public class AutorouteQueryObjectType : ObjectGraphType<AutorouteFieldSet>
{
    public AutorouteQueryObjectType(IStringLocalizer<AutorouteQueryObjectType> S)
    {
        Name = "AutorouteFieldSet";
        Description = S["Custom URLs (permalinks) for your content item."];

        Field(x => x.Path).Description(S["The permalinks for your content item."]);
    }
}
