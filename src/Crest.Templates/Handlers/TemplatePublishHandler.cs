using Crest.ContentManagement.Handlers;
using Crest.Environment.Cache;
using Crest.Templates.Models;

namespace Crest.Templates.Handlers;

/// <summary>Re-reads the published template set when a template's published version changes.</summary>
public sealed class TemplatePublishHandler(ISignal signal) : ContentHandlerBase
{
    public override Task PublishedAsync(PublishContentContext context) => SignalAsync(context.ContentItem.ContentType);

    public override Task UnpublishedAsync(PublishContentContext context) => SignalAsync(context.ContentItem.ContentType);

    public override Task RemovedAsync(RemoveContentContext context) => SignalAsync(context.ContentItem.ContentType);

    private Task SignalAsync(string contentType) =>
        contentType == TemplatesConstants.ContentType ? signal.SignalTokenAsync(TemplatesConstants.PublishedSignal) : Task.CompletedTask;
}
