using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL.Queries;
using Grove.Contents.Localization.Models;

namespace Grove.Contents.Localization.GraphQL;

public class LocalizationInputObjectType : WhereInputObjectGraphType<LocalizationFieldSet>
{
    public LocalizationInputObjectType(IStringLocalizer<LocalizationInputObjectType> stringLocalizer)
        : base(stringLocalizer)
    {
        Name = "LocalizationInputObjectType";
        Description = S["the localization part of the content item"];

        AddScalarFilterFields<StringGraphType>("culture", S["the culture of the content item to filter"]);
        Field(x => x.LocalizationSet, nullable: true).Description(S["the localization set of the content item to filter"]);
    }
}
