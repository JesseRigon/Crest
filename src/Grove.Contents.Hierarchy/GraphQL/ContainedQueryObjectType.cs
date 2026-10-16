using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Management;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Hierarchy.Models;

namespace Grove.Contents.Hierarchy.GraphQL;

public class ContainedQueryObjectType : ObjectGraphType<ParentContentItemFieldSet>
{
    public ContainedQueryObjectType(IStringLocalizer<ContainedQueryObjectType> S)
    {
        Name = nameof(ParentContentItemFieldSet);
        Description = S["Represents a link to the parent content item and the order in which the current content item is represented."];

        Field(x => x.ListContentItemId);

        Field<ContentItemInterface, ContentItem>("listContentItem")
           .Description(S["the parent list content item"])
           .ResolveLockedAsync(async x =>
           {
               var contentItemId = x.Source.ListContentItemId;
               var contentManager = x.RequestServices.GetService<IContentManager>();

               return await contentManager.GetAsync(contentItemId);
           });

        Field(x => x.ListContentType)
            .Description(S["the content type of the list owning the current content item."]);

        Field(x => x.Order)
            .Description(S["the order of the current content item in the list."]);
    }
}
