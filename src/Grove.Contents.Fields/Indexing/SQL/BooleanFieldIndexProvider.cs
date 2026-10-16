using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using YesSql.Indexes;

namespace Grove.Contents.Fields.Indexing.SQL;

public class BooleanFieldIndex : ContentFieldIndex
{
    public bool? Boolean { get; set; }
}

public class BooleanFieldIndexProvider : ContentFieldIndexProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HashSet<string> _ignoredTypes = [];
    private IContentDefinitionManager _contentDefinitionManager;

    public BooleanFieldIndexProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<BooleanFieldIndex>()
            .Map(async contentItem =>
            {
                // Remove index records of soft deleted items.
                if (!contentItem.Published && !contentItem.Latest)
                {
                    return null;
                }

                // Can we safely ignore this content item?
                if (_ignoredTypes.Contains(contentItem.ContentType))
                {
                    return null;
                }

                // Lazy initialization because of ISession cyclic dependency
                _contentDefinitionManager ??= _serviceProvider.GetRequiredService<IContentDefinitionManager>();

                // Search for BooleanField
                var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

                // This can occur when content items become orphaned, particularly layer widgets when a layer is removed, before its widgets have been unpublished.
                if (contentTypeDefinition == null)
                {
                    _ignoredTypes.Add(contentItem.ContentType);
                    return null;
                }

                var fieldDefinitions = contentTypeDefinition
                    .FieldSets.SelectMany(x => x.FieldSetDefinition.Fields.Where(f => f.FieldTypeDefinition.Name == nameof(BooleanField)))
                    .ToArray();

                // This type doesn't have any BooleanField, ignore it
                if (fieldDefinitions.Length == 0)
                {
                    _ignoredTypes.Add(contentItem.ContentType);
                    return null;
                }

                return fieldDefinitions
                    .GetContentFields<BooleanField>(contentItem)
                    .Select(pair =>
                        new BooleanFieldIndex
                        {
                            Latest = contentItem.Latest,
                            Published = contentItem.Published,
                            ContentItemId = contentItem.ContentItemId,
                            ContentItemVersionId = contentItem.ContentItemVersionId,
                            ContentType = contentItem.ContentType,
                            ContentFieldSet = pair.Definition.ContentTypeFieldSetDefinition.Name,
                            ContentField = pair.Definition.Name,
                            Boolean = pair.Field.Value,
                        });
            });
    }
}
