using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL.Queries;
using Grove.Contents.Lists.Models;

namespace Grove.Contents.Lists.GraphQL;

public class ContainedInputObjectType : WhereInputObjectGraphType<ContainedPart>
{
    public ContainedInputObjectType(IStringLocalizer<ContainedInputObjectType> stringLocalizer)
        : base(stringLocalizer)
    {
        Name = "ContainedPartInput";
        Description = S["the list part of the content item"];

        AddScalarFilterFields<IdGraphType>("listContentItemId", S["the content item id of the parent list of the content item to filter"]);
    }
}
