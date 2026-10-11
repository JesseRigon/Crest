using Microsoft.AspNetCore.Http;
using Crest.ContentManagement;
using Crest.ContentManagement.Handlers;
using Crest.ContentPreview;
using Crest.Indexing;
using Crest.Indexing.Models;

namespace Crest.Indexing;

public sealed class CreateIndexingTaskContentHandler : ContentHandlerBase
{
    private readonly IIndexingTaskManager _indexingTaskManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateIndexingTaskContentHandler(
        IIndexingTaskManager indexingTaskManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _indexingTaskManager = indexingTaskManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task UpdatedAsync(UpdateContentContext context)
        => AddUpdateTaskAsync(context.ContentItem);

    public override Task CreatedAsync(CreateContentContext context)
        => AddUpdateTaskAsync(context.ContentItem);

    public override Task PublishedAsync(PublishContentContext context)
        => AddUpdateTaskAsync(context.ContentItem);

    public override Task RemovedAsync(RemoveContentContext context)
    {
        if (!context.NoActiveVersionLeft)
        {
            return Task.CompletedTask;
        }

        return _indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(context.ContentItem.ContentItemId, IndexingConstants.ContentsIndexSource, RecordIndexingTaskTypes.Delete));
    }

    private Task AddUpdateTaskAsync(ContentItem contentItem)
    {
        // Do not index a preview content item.
        if (_httpContextAccessor.HttpContext?.Features.Get<ContentPreviewFeature>()?.Previewing == true)
        {
            return Task.CompletedTask;
        }

        if (contentItem.Id == 0)
        {
            // Ignore that case, when Update is called on a content item which has not be "created" yet.
            return Task.CompletedTask;
        }

        return _indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(contentItem.ContentItemId, IndexingConstants.ContentsIndexSource, RecordIndexingTaskTypes.Update));
    }
}
