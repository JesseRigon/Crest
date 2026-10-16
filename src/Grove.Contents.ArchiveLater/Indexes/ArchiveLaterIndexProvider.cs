using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.ArchiveLater.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Data;
using YesSql.Indexes;

namespace Grove.Contents.ArchiveLater.Indexes;

public class ArchiveLaterIndexProvider : ContentHandlerBase, IIndexProvider, IScopedIndexProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HashSet<string> _partRemoved = [];
    private IContentDefinitionManager _contentDefinitionManager;

    public ArchiveLaterIndexProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task UpdatedAsync(UpdateContentContext context)
    {
        if (context.ContentItem.TryGet<ArchiveLaterFieldSet>(out _))
        {
            _contentDefinitionManager ??= _serviceProvider.GetRequiredService<IContentDefinitionManager>();

            var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);
            if (!contentTypeDefinition.FieldSets.Any(pd => pd.Name == ContentFieldSetName.Of<ArchiveLaterFieldSet>()))
            {
                context.ContentItem.Remove<ArchiveLaterFieldSet>();
                _partRemoved.Add(context.ContentItem.ContentItemId);
            }
        }
    }

    public string CollectionName { get; set; }

    public Type ForType() => typeof(ContentItem);

    public void Describe(IDescriptor context) => Describe((DescribeContext<ContentItem>)context);

    public void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<ArchiveLaterIndex>()
            .When(contentItem => contentItem.Has<ArchiveLaterFieldSet>() || _partRemoved.Contains(contentItem.ContentItemId))
            .Map(contentItem =>
            {
                if (!contentItem.Published || !contentItem.Latest)
                {
                    return null;
                }

                if (!contentItem.TryGet<ArchiveLaterFieldSet>(out var part) || !part.ScheduledArchiveUtc.HasValue)
                {
                    return null;
                }

                return new ArchiveLaterIndex
                {
                    ContentItemId = part.ContentItem.ContentItemId,
                    ScheduledArchiveDateTimeUtc = part.ScheduledArchiveUtc,
                    Published = part.ContentItem.Published,
                    Latest = part.ContentItem.Latest,
                };
            });
    }
}
