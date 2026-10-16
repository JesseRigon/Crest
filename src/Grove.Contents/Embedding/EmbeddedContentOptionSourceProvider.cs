using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Records;
using Grove.Models;
using Grove.Services;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Services;

namespace Grove.Contents.Embedding;

/// <summary>
/// The items of a referenceable embedding as an option source (docs/content-engine.md › Embedded
/// content items): <c>embedded:{Type}.{Embedding}</c> offers the embedded items of every published
/// item of the type the caller may see, by their stable ids. Parents are filtered through the
/// content item source, so what the caller may see bounds what is offered. An embedding not
/// declared referenceable offers nothing. Nested items carry their place in the tree (their path
/// of names, whether they are a leaf) and their page; filtering on <see cref="Columns.Parent"/>
/// scopes the source to one parent item (a taxonomy field's terms). When the module that owns the
/// embedding registers an <see cref="IEmbeddedContentItemFactory"/>, a field may add items by name.
/// </summary>
public sealed class EmbeddedContentOptionSourceProvider(
    ISession session,
    IContentDefinitionManager definitions,
    IServiceProvider services) : IOptionSourceProvider, IOptionSourceInlineCreate
{
    public const string ProviderKey = OptionSourceKeys.EmbeddedProvider;

    // Generous: the items are read from their parents' documents.
    private const int MaxParents = 1000;

    public string Key => ProviderKey;

    public static class Columns
    {
        public const string DisplayText = "DisplayText";
        public const string ContentType = "ContentType";
        public const string Parent = "Parent";
        public const string ParentDisplayText = "ParentDisplayText";

        /// <summary>The names from the embedding's root down to the item, joined by " / ".</summary>
        public const string Path = "Path";

        /// <summary>"true" for an item with no nested items, else "false".</summary>
        public const string Leaf = "Leaf";
    }

    public Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OptionSourceColumnDescriptor>>(
        [
            new(Columns.DisplayText, "Display text", IsDefaultDisplay: true),
            new(Columns.ContentType, "Content type"),
            new(Columns.Parent, "Parent id"),
            new(Columns.ParentDisplayText, "Parent"),
            new(Columns.Path, "Path"),
            new(Columns.Leaf, "Leaf"),
        ]);

    public async Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default)
    {
        if (await EmbeddingAsync(query.Qualifier) is not { } target)
        {
            return [];
        }

        var parents = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == target.Type.Name && index.Published)
            .OrderBy(index => index.DisplayText)
            .Take(MaxParents)
            .ListAsync(cancellationToken);

        IEnumerable<OptionRow> rows = (await VisibleAsync(target.Type.Name, parents, cancellationToken))
            .SelectMany(parent => Rows(parent, target.Embedding, query.Columns));
        rows = OptionFilterMatcher.Apply(rows, query.Filters);
        rows = OptionFilterMatcher.Search(rows, query.SearchText, query.SearchColumns is { Count: > 0 } ? query.SearchColumns : [Columns.DisplayText]);
        if (query.SortColumns is { Count: > 0 })
        {
            rows = OptionRowSorter.Apply(rows, query.SortColumns);
        }

        return [.. rows.Skip(query.Skip).Take(query.Take)];
    }

    public async Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0 || await EmbeddingAsync(qualifier) is not { } target)
        {
            return [];
        }

        var wanted = ids.ToHashSet(StringComparer.Ordinal);
        var parents = await session.Query<ContentItem, EmbeddedContentItemIndex>(index =>
                index.EmbeddingName == target.Embedding.Name && index.ContentItemId.IsIn(wanted) && index.Published)
            .With<ContentItemIndex>(index => index.ContentType == target.Type.Name && index.Published)
            .ListAsync(cancellationToken);

        return [.. (await VisibleAsync(target.Type.Name, parents.DistinctBy(parent => parent.ContentItemId), cancellationToken))
            .SelectMany(parent => Rows(parent, target.Embedding, columns).Where(row => wanted.Contains(row.Id)))];
    }

    public async Task<OptionInlineCreateResult> CreateAsync(string qualifier, IReadOnlyList<ResolvedOptionFilter> filters, string displayText, CancellationToken cancellationToken = default)
    {
        if (await EmbeddingAsync(qualifier) is not { } target)
        {
            return OptionInlineCreateResult.Refused("The source offers no items.");
        }

        // The parent the attachment scopes the source to is where the item goes.
        var parentId = filters.FirstOrDefault(filter => string.Equals(filter.Path, Columns.Parent, StringComparison.OrdinalIgnoreCase)
            && filter.Operator is OptionFilterOperators.Equals or OptionFilterOperators.In && filter.Values.Count == 1)?.Values[0];
        var factory = services.GetServices<IEmbeddedContentItemFactory>().FirstOrDefault(candidate =>
            string.Equals(candidate.ContentType, target.Type.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Embedding, target.Embedding.Name, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(parentId) || factory is null)
        {
            return OptionInlineCreateResult.Refused("Items cannot be added to this source from a field.");
        }

        var created = await factory.CreateAsync(parentId, displayText, cancellationToken);
        if (created.Forbidden)
        {
            return OptionInlineCreateResult.Denied();
        }

        return created.Item is { } item && created.Errors.Count == 0
            ? OptionInlineCreateResult.Created(new OptionRow(item.ContentItemId, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Columns.DisplayText] = item.DisplayText,
                [Columns.Path] = item.DisplayText,
                [Columns.Parent] = parentId,
                [Columns.Leaf] = "true",
            }) { Key = item.ContentItemId })
            : OptionInlineCreateResult.Refused([.. created.Errors]);
    }

    // Every item of the embedding under the parent, depth first, with its path of names and whether it is a leaf.
    private static IEnumerable<OptionRow> Rows(ContentItem parent, EmbeddedContentDefinition embedding, IReadOnlyList<string> columns)
    {
        return Walk(parent.Embedded(embedding.Name), null);

        IEnumerable<OptionRow> Walk(IEnumerable<ContentItem> items, string path)
        {
            foreach (var item in items)
            {
                var itemPath = path is null ? item.DisplayText : $"{path} / {item.DisplayText}";
                var children = embedding.Nested ? item.Embedded(embedding.Name) : [];
                yield return ToRow(parent, item, itemPath, children.Count == 0, columns);
                foreach (var row in Walk(children, itemPath))
                {
                    yield return row;
                }
            }
        }
    }

    // The qualifier's type and its referenceable embedding.
    private async Task<(ContentTypeDefinition Type, EmbeddedContentDefinition Embedding)?> EmbeddingAsync(string qualifier)
    {
        var separator = qualifier?.IndexOf('.') ?? -1;
        if (separator <= 0)
        {
            return null;
        }

        var type = await definitions.GetTypeDefinitionAsync(qualifier[..separator]);
        var embedding = type?.Embeddings.FirstOrDefault(candidate => string.Equals(candidate.Name, qualifier[(separator + 1)..], StringComparison.OrdinalIgnoreCase));
        return embedding is { Referenceable: true } ? (type, embedding) : null;
    }

    // The parents the caller may see: the content item source answers only those.
    private async Task<IReadOnlyList<ContentItem>> VisibleAsync(string contentType, IEnumerable<ContentItem> parents, CancellationToken cancellationToken)
    {
        var candidates = parents.ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        var contentItems = services.GetServices<IOptionSourceProvider>().FirstOrDefault(provider => provider.Key == OptionSourceKeys.ContentItemProvider);
        if (contentItems is null)
        {
            return [];
        }

        var visible = (await contentItems.GetByIdsAsync(contentType, [.. candidates.Select(parent => parent.ContentItemId)], [], cancellationToken))
            .Select(row => row.Id)
            .ToHashSet(StringComparer.Ordinal);
        return [.. candidates.Where(parent => visible.Contains(parent.ContentItemId))];
    }

    private static OptionRow ToRow(ContentItem parent, ContentItem item, string path, bool leaf, IReadOnlyList<string> columns)
    {
        var requested = columns is { Count: > 0 } ? columns : [Columns.DisplayText];
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in requested)
        {
            values[column] = column switch
            {
                Columns.DisplayText => item.DisplayText,
                Columns.ContentType => item.ContentType,
                Columns.Parent => parent.ContentItemId,
                Columns.ParentDisplayText => parent.DisplayText,
                Columns.Path => path,
                Columns.Leaf => leaf ? "true" : "false",
                _ => null,
            };
        }

        return new OptionRow(item.ContentItemId, values) { Key = item.ContentItemId, Url = Grove.Contents.Fields.ViewModels.ContentItemLinks.SitePath(item) };
    }
}
