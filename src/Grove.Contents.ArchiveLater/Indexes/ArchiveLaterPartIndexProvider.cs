using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.ArchiveLater.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Data;
using YesSql.Indexes;

namespace Grove.Contents.ArchiveLater.Indexes;

public class ArchiveLaterPartIndexProvider : ContentHandlerBase, IIndexProvider, IScopedIndexProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HashSet<string> _partRemoved = [];
    private IContentDefinitionManager _contentDefinitionManager;

    public ArchiveLaterPartIndexProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task UpdatedAsync(UpdateContentContext context)
    {
        if (context.ContentItem.TryGet<ArchiveLaterPart>(out _))
        {
            _contentDefinitionManager ??= _serviceProvider.GetRequiredService<IContentDefinitionManager>();

            var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);
            if (!contentTypeDefinition.Parts.Any(pd => pd.Name == nameof(ArchiveLaterPart)))
            {
                context.ContentItem.Remove<ArchiveLaterPart>();
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
            .For<ArchiveLaterPartIndex>()
            .When(contentItem => contentItem.Has<ArchiveLaterPart>() || _partRemoved.Contains(contentItem.ContentItemId))
            .Map(contentItem =>
            {
                if (!contentItem.Published || !contentItem.Latest)
                {
                    return null;
                }

                if (!contentItem.TryGet<ArchiveLaterPart>(out var part) || !part.ScheduledArchiveUtc.HasValue)
                {
                    return null;
                }

                return new ArchiveLaterPartIndex
                {
                    ContentItemId = part.ContentItem.ContentItemId,
                    ScheduledArchiveDateTimeUtc = part.ScheduledArchiveUtc,
                    Published = part.ContentItem.Published,
                    Latest = part.ContentItem.Latest,
                };
            });
    }
}
