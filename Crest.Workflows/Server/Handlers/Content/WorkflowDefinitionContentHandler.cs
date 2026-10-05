using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Mappers;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Management.Notifications;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using Crest.Workflows.Parts;
using Crest.Workflows.Services;

namespace Crest.Workflows.Handlers.Content;

public class WorkflowDefinitionContentHandler(IMediator mediator, IServiceProvider serviceProvider
) : ContentHandlerBase
{
    private readonly Lazy<WorkflowDefinitionPartMapper> _workflowDefinitionPartMapper = new(serviceProvider.GetRequiredService<WorkflowDefinitionPartMapper>);
    private readonly Lazy<WorkflowDefinitionMapper> _workflowDefinitionMapper = new(serviceProvider.GetRequiredService<WorkflowDefinitionMapper>());
    private readonly Lazy<WorkflowDefinitionPartSerializer> _workflowDefinitionPartSerializer = new(serviceProvider.GetRequiredService<WorkflowDefinitionPartSerializer>);
    private WorkflowDefinitionPartMapper WorkflowDefinitionPartMapper => _workflowDefinitionPartMapper.Value;
    private WorkflowDefinitionMapper WorkflowDefinitionMapper => _workflowDefinitionMapper.Value;
    private WorkflowDefinitionPartSerializer WorkflowDefinitionPartSerializer => _workflowDefinitionPartSerializer.Value;

    public override Task UpdatingAsync(UpdateContentContext context)
    {
        if (!context.ContentItem.Has<WorkflowDefinitionPart>())
            return Task.CompletedTask;
        
        var workflowDefinitionPart = context.ContentItem.Get<WorkflowDefinitionPart>(typeof(WorkflowDefinitionPart).Name);
        context.ContentItem.DisplayText = workflowDefinitionPart.Name;
        return Task.CompletedTask;
    }

    public override async Task PublishingAsync(PublishContentContext context)
    {
        if (!context.ContentItem.Has<WorkflowDefinitionPart>())
            return;

        var newItem = context.ContentItem;
        var previousItem = context.PreviousItem;
        
        await newItem.AlterAsync<WorkflowDefinitionPart>(async part =>
        {
            part.DefinitionVersionId = newItem.ContentItemVersionId;
            //part.IsLatest = true;
            //part.IsPublished = true;
            newItem.DisplayText = part.Name;
            var definitionModel = WorkflowDefinitionPartSerializer.UpdateSerializedData(part);
            var definition = WorkflowDefinitionMapper.MapToWorkflowDefinition(definitionModel);
            await mediator.SendAsync(new WorkflowDefinitionPublishing(definition));

            // Handlers may stamp the version being published (field warnings): what they put on
            // the definition's custom properties is what gets stored.
            definitionModel.CustomProperties = definition.CustomProperties;
            WorkflowDefinitionPartSerializer.Write(part, definitionModel);
        });

        // previousItem?.Alter<WorkflowDefinitionPart>(part =>
        // {
        //     part.IsPublished = false;
        //     part.IsLatest = false;
        //     WorkflowDefinitionPartSerializer.UpdateSerializedData(part);
        // });
    }

    public override async Task PublishedAsync(PublishContentContext context)
    {
        if (!context.ContentItem.Has<WorkflowDefinitionPart>())
            return;

        var previousItem = context.PreviousItem;

        if (previousItem != null)
        {
            var previousDefinitionPart = previousItem.Get<WorkflowDefinitionPart>(typeof(WorkflowDefinitionPart).Name);
            var previousDefinition = WorkflowDefinitionPartMapper.Map(previousDefinitionPart);
            await mediator.SendAsync(new WorkflowDefinitionVersionRetracted(previousDefinition));
        }
        
        var workflowDefinitionPart = context.ContentItem.Get<WorkflowDefinitionPart>(typeof(WorkflowDefinitionPart).Name);
        var workflowDefinition = WorkflowDefinitionPartMapper.Map(workflowDefinitionPart);
        var affectedWorkflows = new AffectedWorkflows(new List<WorkflowDefinition>());
        await mediator.SendAsync(new WorkflowDefinitionPublished(workflowDefinition, affectedWorkflows));
    }

    public override async Task UnpublishingAsync(PublishContentContext context)
    {
        if (!context.ContentItem.Has<WorkflowDefinitionPart>())
            return;

        // context.ContentItem.Alter<WorkflowDefinitionPart>(part =>
        // {
        //     part.IsPublished = false;
        //     part.IsLatest = false;
        //     WorkflowDefinitionPartSerializer.UpdateSerializedData(part);
        // });

        // The Retracted notification drives trigger indexing: it must carry the definition as
        // unpublished - the content item still says Published here, Unpublishing being before
        // the fact - or the indexer re-indexes it as published and a retracted flow keeps its
        // triggers (and keeps firing).
        var workflowDefinitionPart = context.ContentItem.Get<WorkflowDefinitionPart>(typeof(WorkflowDefinitionPart).Name);
        var workflowDefinition = WorkflowDefinitionPartMapper.Map(workflowDefinitionPart);
        workflowDefinition.IsPublished = false;
        await mediator.SendAsync(new WorkflowDefinitionRetracting(workflowDefinition));
        await mediator.SendAsync(new WorkflowDefinitionRetracted(workflowDefinition));
    }

    public override async Task RemovingAsync(RemoveContentContext context)
    {
        if (!context.ContentItem.Has<WorkflowDefinitionPart>())
            return;
        
        var workflowDefinitionPart = context.ContentItem.Get<WorkflowDefinitionPart>(typeof(WorkflowDefinitionPart).Name);
        await mediator.SendAsync(new WorkflowDefinitionDeleting(workflowDefinitionPart.DefinitionId));
    }
}