using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Contents.Alias.Models;
using Grove.Data.Protocols.GraphQL.Queries;

namespace Grove.Contents.Alias.GraphQL;

public class AliasInputObjectType : WhereInputObjectGraphType<AliasFieldSet>
{
    public AliasInputObjectType(IStringLocalizer<AliasInputObjectType> stringLocalizer)
        : base(stringLocalizer)
    {
        Name = "AliasFieldSetInput";
        Description = S["the alias part of the content item"];

        AddScalarFilterFields<StringGraphType>("alias", S["the alias of the content item"]);
    }
}
