using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Management.Mappers;
using Grove.Workflows.Management.Models;
using Grove.Workflows.Management.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Workflows.FieldSets;
using Grove.Workflows.Services;
using Grove.Environment.Cache;

namespace Grove.Workflows.Handlers.Content;

public class WorkflowDefinitionContentHandler(IMediator mediator, IServiceProvider serviceProvider
) : ContentHandlerBase
{
    private readonly Lazy<WorkflowDefinitionFieldSetMapper> _workflowDefinitionFieldSetMapper = new(serviceProvider.GetRequiredService<WorkflowDefinitionFieldSetMapper>);
    private readonly Lazy<WorkflowDefinitionMapper> _workflowDefinitionMapper = new(serviceProvider.GetRequiredService<WorkflowDefinitionMapper>());
    private readonly Lazy<WorkflowDefinitionFieldSetSerializer> _workflowDefinitionFieldSetSerializer = new(serviceProvider.GetRequiredService<WorkflowDefinitionFieldSetSerializer>);
    private WorkflowDefinitionFieldSetMapper WorkflowDefinitionFieldSetMapper => _workflowDefinitionFieldSetMapper.Value;
    private WorkflowDefinitionMapper WorkflowDefinitionMapper => _workflowDefinitionMapper.Value;
    private WorkflowDefinitionFieldSetSerializer WorkflowDefinitionFieldSetSerializer => _workflowDefinitionFieldSetSerializer.Value;

    public override Task UpdatingAsync(UpdateContentContext context)
    {
        if (!context.ContentItem.Has<WorkflowDefinitionFieldSet>())
            return Task.CompletedTask;
        
        var workflowDefinitionFieldSet = context.ContentItem.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>());
        context.ContentItem.DisplayText = workflowDefinitionFieldSet.Name;
        return Task.CompletedTask;
    }

    public override async Task PublishingAsync(PublishContentContext context)
    {
        if (!context.ContentItem.Has<WorkflowDefinitionFieldSet>())
            return;

        var newItem = context.ContentItem;
        var previousItem = context.PreviousItem;
        
        await newItem.AlterAsync<WorkflowDefinitionFieldSet>(async part =>
        {
            part.DefinitionVersionId = newItem.ContentItemVersionId;
            //part.IsLatest = true;
            //part.IsPublished = true;
            newItem.DisplayText = part.Name;
            var definitionModel = WorkflowDefinitionFieldSetSerializer.UpdateSerializedData(part);
            var definition = WorkflowDefinitionMapper.MapToWorkflowDefinition(definitionModel);
            await mediator.SendAsync(new WorkflowDefinitionPublishing(definition));

            // Handlers may stamp the version being published (field warnings): what they put on
            // the definition's custom properties is what gets stored.
            definitionModel.CustomProperties = definition.CustomProperties;
            WorkflowDefinitionFieldSetSerializer.Write(part, definitionModel);
        });

        // previousItem?.Alter<WorkflowDefinitionFieldSet>(part =>
        // {
        //     part.IsPublished = false;
        //     part.IsLatest = false;
        //     WorkflowDefinitionFieldSetSerializer.UpdateSerializedData(part);
        // });
    }

    public override async Task PublishedAsync(PublishContentContext context)
    {
        if (!context.ContentItem.Has<WorkflowDefinitionFieldSet>())
            return;

        var previousItem = context.PreviousItem;

        if (previousItem != null)
        {
            var previousDefinitionPart = previousItem.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>());
            var previousDefinition = WorkflowDefinitionFieldSetMapper.Map(previousDefinitionPart);
            await mediator.SendAsync(new WorkflowDefinitionVersionRetracted(previousDefinition));
        }
        
        var workflowDefinitionFieldSet = context.ContentItem.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>());
        var workflowDefinition = WorkflowDefinitionFieldSetMapper.Map(workflowDefinitionFieldSet);
        var affectedWorkflows = new AffectedWorkflows(new List<WorkflowDefinition>());
        await mediator.SendAsync(new WorkflowDefinitionPublished(workflowDefinition, affectedWorkflows));
        InvalidateHookAttachments();
    }

    public override async Task UnpublishingAsync(PublishContentContext context)
    {
        if (!context.ContentItem.Has<WorkflowDefinitionFieldSet>())
            return;

        // context.ContentItem.Alter<WorkflowDefinitionFieldSet>(part =>
        // {
        //     part.IsPublished = false;
        //     part.IsLatest = false;
        //     WorkflowDefinitionFieldSetSerializer.UpdateSerializedData(part);
        // });

        // The Retracted notification drives trigger indexing: it must carry the definition as
        // unpublished - the content item still says Published here, Unpublishing being before
        // the fact - or the indexer re-indexes it as published and a retracted flow keeps its
        // triggers (and keeps firing).
        var workflowDefinitionFieldSet = context.ContentItem.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>());
        var workflowDefinition = WorkflowDefinitionFieldSetMapper.Map(workflowDefinitionFieldSet);
        workflowDefinition.IsPublished = false;
        await mediator.SendAsync(new WorkflowDefinitionRetracting(workflowDefinition));
        await mediator.SendAsync(new WorkflowDefinitionRetracted(workflowDefinition));
    }

    public override async Task RemovingAsync(RemoveContentContext context)
    {
        if (!context.ContentItem.Has<WorkflowDefinitionFieldSet>())
            return;
        
        var workflowDefinitionFieldSet = context.ContentItem.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>());
        await mediator.SendAsync(new WorkflowDefinitionDeleting(workflowDefinitionFieldSet.DefinitionId));
        InvalidateHookAttachments();
    }

    // Hook slots list flows by definition (names, installed system flows): re-resolve them.
    private void InvalidateHookAttachments() =>
        serviceProvider.GetRequiredService<Grove.Environment.Cache.ISignal>().DeferredSignalToken(Hooks.WorkflowHookAttachmentCache.SignalKey);
}