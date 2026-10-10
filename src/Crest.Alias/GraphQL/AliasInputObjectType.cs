using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Crest.Alias.Models;
using Crest.Apis.GraphQL.Queries;

namespace Crest.Alias.GraphQL;

public class AliasInputObjectType : WhereInputObjectGraphType<AliasPart>
{
    public AliasInputObjectType(IStringLocalizer<AliasInputObjectType> stringLocalizer)
        : base(stringLocalizer)
    {
        Name = "AliasPartInput";
        Description = S["the alias part of the content item"];

        AddScalarFilterFields<StringGraphType>("alias", S["the alias of the content item"]);
    }
}
