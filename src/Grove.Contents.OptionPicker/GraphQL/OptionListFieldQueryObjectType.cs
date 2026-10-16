using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Management;
using Grove.Contents.Management.GraphQL;
using Grove.Contents.Management.GraphQL.Queries;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Fields;
using Grove.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Grove.Contents.OptionPicker.GraphQL;

/// <summary>
/// An option picker field in GraphQL: its source, its selected ids and, for the content items
/// source, the selected items themselves (in the stored order); for an embedded items source (a
/// taxonomy's terms), the selected embedded items.
/// </summary>
public sealed class OptionListFieldQueryObjectType : ObjectGraphType<OptionListField>
{
    public OptionListFieldQueryObjectType(IStringLocalizer<OptionListFieldQueryObjectType> S)
    {
        Name = nameof(OptionListField);

        Field<StringGraphType, string>("sourceKey")
            .Description(S["the option source"])
            .Resolve(context => context.Source.SourceKey);

        Field<ListGraphType<StringGraphType>, IEnumerable<string>>("selectedIds")
            .Description(S["the selected ids"])
            .PagingArguments()
            .Resolve(context => context.Page(context.Source.SelectedIds));

        Field<ListGraphType<ContentItemInterface>, IEnumerable<ContentItem>>("contentItems")
            .Description(S["the selected content items, for a content item source"])
            .PagingArguments()
            .Argument<PublicationStatusGraphType>("status", argument =>
            {
                argument.Description = S["publication status of the content item"];
                argument.DefaultValue = PublicationStatusEnum.Published;
            })
            .ResolveAsync(IDataLoaderResult<IEnumerable<ContentItem>> (context) =>
            {
                // A value stored without its source key (imported) is read as content items when
                // the ids resolve; ids of another source resolve to nothing.
                var source = OptionSourceKeys.Split(context.Source.SourceKey).Provider;
                if (source.Length > 0 && source != OptionSourceKeys.ContentItemProvider)
                {
                    return new DataLoaderResult<IEnumerable<ContentItem>>([]);
                }

                var loader = context.GetOrAddContentItemByIdDataLoader();
                return loader.LoadAsync(context.Page(context.Source.SelectedIds)).Then(items => (IEnumerable<ContentItem>)items.SelectMany(item => item));
            });

        Field<ListGraphType<ContentItemInterface>, IEnumerable<ContentItem>>("embeddedItems")
            .Description(S["the selected embedded content items (a taxonomy's terms), for an embedded items source"])
            .PagingArguments()
            .ResolveAsync(async context =>
            {
                // Each id resolves to the item in its parent's published version; ids of another
                // source resolve to nothing.
                var source = OptionSourceKeys.Split(context.Source.SourceKey).Provider;
                if (source.Length > 0 && source != OptionSourceKeys.EmbeddedProvider)
                {
                    return [];
                }

                var resolver = context.RequestServices!.GetRequiredService<IEmbeddedContentItemResolver>();
                var items = new List<ContentItem>();
                foreach (var id in context.Page(context.Source.SelectedIds))
                {
                    if (await resolver.ResolveAsync(id, cancellationToken: context.CancellationToken) is { } reference)
                    {
                        items.Add(reference.Item);
                    }
                }

                return items;
            });
    }
}
