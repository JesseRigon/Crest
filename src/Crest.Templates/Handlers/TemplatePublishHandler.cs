using Crest.ContentManagement.Handlers;
using Crest.Environment.Cache;
using Crest.Templates.Models;

namespace Crest.Templates.Handlers;

/// <summary>Re-reads the published templates, or the published designed pages, when one's published version changes.</summary>
public sealed class TemplatePublishHandler(ISignal signal) : ContentHandlerBase
{
    public override Task PublishedAsync(PublishContentContext context) => SignalAsync(context.ContentItem.ContentType);

    public override Task UnpublishedAsync(PublishContentContext context) => SignalAsync(context.ContentItem.ContentType);

    public override Task RemovedAsync(RemoveContentContext context) => SignalAsync(context.ContentItem.ContentType);

    private Task SignalAsync(string contentType) => contentType switch
    {
        TemplatesConstants.ContentType => signal.SignalTokenAsync(TemplatesConstants.PublishedSignal),
        DesignedPagesConstants.ContentType => signal.SignalTokenAsync(DesignedPagesConstants.PublishedSignal),
        _ => Task.CompletedTask,
    };
}
