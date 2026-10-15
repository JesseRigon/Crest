using GraphQL.Types;
using Grove.ContentManagement.GraphQL.Queries.Types;
using Grove.ContentManagement.Metadata.Models;
using Grove.Lists.Models;

namespace Grove.Lists.GraphQL;

internal sealed class ContainedPartContentTypeBuilder : IContentTypeBuilder
{
    public void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType)
    {
        foreach (var listPart in contentTypeDefinition.Parts.Where(p => p.PartDefinition.Name.Equals(nameof(ListPart), StringComparison.OrdinalIgnoreCase)))
        {
            var settings = listPart?.GetSettings<ListPartSettings>();
            if (settings == null)
            {
                continue;
            }

            if (contentItemType.Metadata.TryGetValue(nameof(ListPartSettings.ContainedContentTypes), out var containedContentTypes) &&
                containedContentTypes is IEnumerable<string> existingContainedContentTypes)
            {
                contentItemType.Metadata[nameof(ListPartSettings.ContainedContentTypes)] = existingContainedContentTypes.Concat(settings.ContainedContentTypes).Distinct().ToArray();
            }
            else
            {
                contentItemType.Metadata[nameof(ListPartSettings.ContainedContentTypes)] = settings.ContainedContentTypes;
            }
        }
    }

    public void Clear()
    {
    }
}
