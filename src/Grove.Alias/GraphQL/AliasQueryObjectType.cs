using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Alias.Models;

namespace Grove.Alias.GraphQL;

public class AliasQueryObjectType : ObjectGraphType<AliasPart>
{
    public AliasQueryObjectType(IStringLocalizer<AliasQueryObjectType> S)
    {
        Name = "AliasPart";
        Description = S["Alternative path for the content item"];

        Field("alias", x => x.Alias, true);
    }
}
