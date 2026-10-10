using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement.Handlers;
using Crest.Contents.Workflows.Activities;
using Crest.Contents.Workflows.Handlers;
using Crest.Modules;
using Crest.Workflows.Platform.Helpers;
using Crest.Workflows.Platform.Services;

namespace Crest.Contents.Workflows;

[RequireFeatures("Crest.Workflows")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<ContentCreatedEvent>();
        services.AddActivity<ContentDeletedEvent>();
        services.AddActivity<ContentPublishedEvent>();
        services.AddActivity<ContentUnpublishedEvent>();
        services.AddActivity<ContentUpdatedEvent>();
        services.AddActivity<ContentDraftSavedEvent>();
        services.AddActivity<ContentVersionedEvent>();
        services.AddActivity<DeleteContentTask>();
        services.AddActivity<PublishContentTask>();
        services.AddActivity<UnpublishContentTask>();
        services.AddActivity<CreateContentTask>();
        services.AddActivity<RetrieveContentTask>();
        services.AddActivity<UpdateContentTask>();

        services.AddScoped<IWorkflowValueSerializer, ContentItemSerializer>();
    }
}

[RequireFeatures("Crest.Workflows")]
public sealed class ContentHandlerStartup : StartupBase
{
    // The order is set to PlatformConstants.ConfigureOrder.WorkflowsContentHandlers to ensure the workflows content
    // handler is registered first in the DI container. This causes the workflows content handler to be invoked last
    // when content events are triggered, allowing it to access the final state of the content item after all other
    // content handlers have executed. Note: handlers are resolved in reverse order, so setting this constant ensures
    // this handler runs last during content item created, updated, etc. events.
    public override int Order => PlatformConstants.ConfigureOrder.WorkflowsContentHandlers;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentHandler, ContentsHandler>();
    }
}
