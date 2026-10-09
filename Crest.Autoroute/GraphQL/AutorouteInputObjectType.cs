using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Crest.Apis.GraphQL.Queries;
using Crest.Autoroute.Models;

namespace Crest.Autoroute.GraphQL;

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
