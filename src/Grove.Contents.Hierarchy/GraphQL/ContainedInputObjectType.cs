using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL.Queries;
using Grove.Contents.Hierarchy.Models;

namespace Grove.Contents.Hierarchy.GraphQL;

public class ContainedInputObjectType : WhereInputObjectGraphType<ParentContentItemFieldSet>
{
    public ContainedInputObjectType(IStringLocalizer<ContainedInputObjectType> stringLocalizer)
        : base(stringLocalizer)
    {
        Name = "ParentContentItemFieldSetInput";
        Description = S["the list part of the content item"];

        AddScalarFilterFields<IdGraphType>("listContentItemId", S["the content item id of the parent list of the content item to filter"]);
    }
}
