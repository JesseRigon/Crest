using GraphQL.Types;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Hierarchy.Models;
using Grove.Contents.Management;

namespace Grove.Contents.Hierarchy.GraphQL;

internal sealed class ParentContentItemFieldSetContentTypeBuilder : IContentTypeBuilder
{
    public void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType)
    {
        foreach (var childContentItemsFieldSet in contentTypeDefinition.FieldSets.Where(p => p.FieldSetDefinition.Name.Equals(ContentFieldSetName.Of<ChildContentItemsFieldSet>(), StringComparison.OrdinalIgnoreCase)))
        {
            var settings = childContentItemsFieldSet?.GetSettings<ChildContentItemsFieldSetSettings>();
            if (settings == null)
            {
                continue;
            }

            if (contentItemType.Metadata.TryGetValue(nameof(ChildContentItemsFieldSetSettings.ContainedContentTypes), out var containedContentTypes) &&
                containedContentTypes is IEnumerable<string> existingContainedContentTypes)
            {
                contentItemType.Metadata[nameof(ChildContentItemsFieldSetSettings.ContainedContentTypes)] = existingContainedContentTypes.Concat(settings.ContainedContentTypes).Distinct().ToArray();
            }
            else
            {
                contentItemType.Metadata[nameof(ChildContentItemsFieldSetSettings.ContainedContentTypes)] = settings.ContainedContentTypes;
            }
        }
    }

    public void Clear()
    {
    }
}
