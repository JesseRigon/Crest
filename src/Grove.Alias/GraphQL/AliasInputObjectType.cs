using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Alias.Models;
using Grove.Apis.GraphQL.Queries;

namespace Grove.Alias.GraphQL;

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
