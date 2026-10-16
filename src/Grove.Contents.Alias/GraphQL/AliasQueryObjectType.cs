using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Contents.Alias.Models;

namespace Grove.Contents.Alias.GraphQL;

public class AliasQueryObjectType : ObjectGraphType<AliasFieldSet>
{
    public AliasQueryObjectType(IStringLocalizer<AliasQueryObjectType> S)
    {
        Name = "AliasFieldSet";
        Description = S["Alternative path for the content item"];

        Field("alias", x => x.Alias, true);
    }
}
