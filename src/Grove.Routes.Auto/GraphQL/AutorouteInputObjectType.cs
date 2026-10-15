using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL.Queries;
using Grove.Routes.Auto.Models;

namespace Grove.Routes.Auto.GraphQL;

public class AutorouteInputObjectType : WhereInputObjectGraphType<AutoroutePart>
{
    public AutorouteInputObjectType(IStringLocalizer<AutorouteInputObjectType> stringLocalizer)
        : base(stringLocalizer)
    {
        Name = "AutoroutePartInput";
        Description = S["the custom URL part of the content item"];

        AddScalarFilterFields<StringGraphType>("path", S["the path of the content item to filter"]);
    }
}
