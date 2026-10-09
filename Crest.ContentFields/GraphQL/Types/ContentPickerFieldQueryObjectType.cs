using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Crest.Apis.GraphQL;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.GraphQL;
using Crest.ContentManagement.GraphQL.Queries;
using Crest.ContentManagement.GraphQL.Queries.Types;

namespace Crest.ContentFields.GraphQL;

public class ContentPickerFieldQueryObjectType : ObjectGraphType<ContentPickerField>
{
    public ContentPickerFieldQueryObjectType(IStringLocalizer<ContentPickerFieldQueryObjectType> S)
    {
        Name = nameof(ContentPickerField);

        Field<ListGraphType<StringGraphType>, IEnumerable<string>>("contentItemIds")
            .Description(S["content item ids"])
            .PagingArguments()
            .Resolve(x =>
            {
                return x.Page(x.Source.ContentItemIds);
            });

        Field<ListGraphType<ContentItemInterface>, IEnumerable<ContentItem>>("contentItems")
            .Description(S["the content items"])
            .PagingArguments()
            .Argument<PublicationStatusGraphType>("status", queryArgument =>
            {
                queryArgument.Description = S["publication status of the content item"];
                queryArgument.DefaultValue = PublicationStatusEnum.Published;
            })
            .ResolveAsync(x =>
            {
                var contentItemLoader = x.GetOrAddContentItemByIdDataLoader();

                return contentItemLoader.LoadAsync(x.Page(x.Source.ContentItemIds)).Then(itemResultSet =>
                {
                    return itemResultSet.SelectMany(x => x);
                });
            });
    }
}
