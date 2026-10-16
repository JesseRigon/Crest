using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Data;
using Grove.Contents.PublishLater.Models;
using YesSql.Indexes;

namespace Grove.Contents.PublishLater.Indexes;

public class PublishLaterIndexProvider : ContentHandlerBase, IIndexProvider, IScopedIndexProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HashSet<string> _partRemoved = [];
    private IContentDefinitionManager _contentDefinitionManager;

    public PublishLaterIndexProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override Task CreatedAsync(CreateContentContext context)
        => UpdatePublishLaterFieldSetAsync(context.ContentItem);

    public override Task UpdatedAsync(UpdateContentContext context)
        => UpdatePublishLaterFieldSetAsync(context.ContentItem);

    private async Task UpdatePublishLaterFieldSetAsync(ContentItem contentItem)
    {
        // Validate that the content definition contains this part, this prevents indexing parts
        // that have been removed from the type definition, but are still present in the elements.            
        if (contentItem.TryGet<PublishLaterFieldSet>(out _))
        {
            // Lazy initialization because of ISession cyclic dependency.
            _contentDefinitionManager ??= _serviceProvider.GetRequiredService<IContentDefinitionManager>();

            // Search for this part.
            var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
            if (!contentTypeDefinition.FieldSets.Any(ctd => ctd.Name == ContentFieldSetName.Of<PublishLaterFieldSet>()))
            {
                contentItem.Remove<PublishLaterFieldSet>();
                _partRemoved.Add(contentItem.ContentItemId);
            }
        }
    }

    public string CollectionName { get; set; }
    public Type ForType() => typeof(ContentItem);
    public void Describe(IDescriptor context) => Describe((DescribeContext<ContentItem>)context);

    public void Describe(DescribeContext<ContentItem> context)
    {
        context.For<PublishLaterIndex>()
            .When(contentItem => contentItem.Has<PublishLaterFieldSet>() || _partRemoved.Contains(contentItem.ContentItemId))
            .Map(contentItem =>
            {
                // Remove index records of items that are already published or not the latest version.
                if (contentItem.Published || !contentItem.Latest)
                {
                    return null;
                }

                if (!contentItem.TryGet<PublishLaterFieldSet>(out var part) || !part.ScheduledPublishUtc.HasValue)
                {
                    return null;
                }

                return new PublishLaterIndex
                {
                    ContentItemId = part.ContentItem.ContentItemId,
                    ScheduledPublishDateTimeUtc = part.ScheduledPublishUtc,
                    Published = part.ContentItem.Published,
                    Latest = part.ContentItem.Latest,
                };
            });
    }
}
