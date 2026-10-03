using Crest.Workflows.Caching;
using Crest.Workflows.Common;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows;
using Crest.Workflows.Activities;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Materializers;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Management.Notifications;
using Crest.Workflows.Management.Stores;
using Crest.Workflows.Models;
using OrchardCore.ContentManagement;
using Crest.Workflows.Parts;
using Crest.Workflows.Registry;

namespace Crest.Workflows.Services;

public class ContentItemWorkflowDefinitionPublisher(
    IContentManager contentManager,
    ISystemClock systemClock,
    IActivitySerializer activitySerializer,
    IWorkflowDefinitionStore workflowDefinitionStore,
    IWorkflowDefinitionService workflowDefinitionService,
    IWorkflowValidator workflowValidator,
    IMediator mediator,
    IWorkflowDefinitionCacheManager workflowDefinitionCacheManager,
    ICacheManager cacheManager,
    WorkflowDefinitionPartMapper workflowDefinitionPartMapper,
    WorkflowDefinitionPartSerializer workflowDefinitionPartSerializer,
    WorkflowOwnershipGuard ownershipGuard) : IWorkflowDefinitionPublisher
{
    public WorkflowDefinition New(IActivity? root = null)
    {
        throw new("Not implemented. Use NewAsync instead.");
    }

    public async Task<WorkflowDefinition> NewAsync(IActivity? root = null, CancellationToken cancellationToken = default)
    {
        const int version = 1;
        root ??= new Sequence();

        var contentItem = await contentManager.NewAsync("WorkflowDefinition");
        var definitionId = contentItem.ContentItemId;

        return new()
        {
            Id = string.Empty,
            DefinitionId = definitionId,
            Version = version,
            IsLatest = true,
            IsPublished = false,
            CreatedAt = systemClock.UtcNow,
            StringData = activitySerializer.Serialize(root),
            MaterializerName = JsonWorkflowMaterializer.MaterializerName
        };
    }

    public async Task<PublishWorkflowDefinitionResult> PublishAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        var filter = WorkflowDefinitionHandle.ByDefinitionId(definitionId, global::Crest.Workflows.Common.Models.VersionOptions.Latest).ToFilter();
        var definition = await workflowDefinitionStore.FindAsync(filter, cancellationToken);

        if (definition == null)
            return new(false, new List<WorkflowValidationError>
            {
                new("Workflow definition not found.")
            }, new([]));

        return await PublishAsync(definition, cancellationToken);
    }

    public async Task<PublishWorkflowDefinitionResult> PublishAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        // The ownership tier decides (plans/workflows.md, phase 5): a system flow is never
        // published by a user, a shipped one keeps its ownership properties whatever the
        // client sent.
        await ownershipGuard.PrepareAsync(definition, WorkflowChange.Publish);
        var workflowGraph = await workflowDefinitionService.MaterializeWorkflowAsync(definition, cancellationToken);
        var validationErrors = (await workflowValidator.ValidateAsync(workflowGraph.Workflow, cancellationToken)).ToList();

        if (validationErrors.Any())
            return new(false, validationErrors, new([]));

        // The engine's Publishing/Published notifications are raised once, by the content
        // handler (WorkflowDefinitionContentHandler) as the content item publishes - not here
        // as well: indexing the triggers twice in one session inserted and then replaced the
        // same documents, which YesSql refuses at flush for a Timer trigger.
        var contentItem = await contentManager.GetAsync(definition.DefinitionId, VersionOptions.DraftRequired);

        if (contentItem == null)
        {
            // Saved with publish=true in one call: NewAsync only allocated the id, nothing
            // is stored yet. Persist the draft first, then publish that content item.
            await workflowDefinitionStore.SaveAsync(definition, cancellationToken);
            contentItem = await contentManager.GetAsync(definition.DefinitionId, VersionOptions.DraftRequired)
                ?? throw new InvalidOperationException($"Workflow definition '{definition.DefinitionId}' could not be stored before publishing.");
        }

        if (definition.IsPublished)
            definition.Version++;

        definition.IsLatest = true;
        definition.IsPublished = true;
        definition.Id = contentItem.ContentItemVersionId;

        contentItem.Alter<WorkflowDefinitionPart>(part =>
        {
            part.DefinitionId = definition.DefinitionId;
            part.DefinitionVersionId = definition.Id;
            // part.IsPublished = definition.IsPublished;
            // part.IsLatest = definition.IsLatest;
            part.Name = definition.Name;
            part.Description = definition.Description;
            part.IsReadonly = definition.IsReadonly;
            part.IsSystem = definition.IsSystem;
            part.MaterializerName = definition.MaterializerName;
            part.ProviderName = definition.ProviderName;
            part.ToolVersion = definition.ToolVersion;
            part.UsableAsActivity = definition.Options.UsableAsActivity == true;
            workflowDefinitionPartSerializer.UpdateSerializedData(part);
        });

        await contentManager.PublishAsync(contentItem);
        await RefreshCacheAsync(definition.DefinitionId, cancellationToken);
        return new(true, validationErrors, new AffectedWorkflows(new List<WorkflowDefinition>()));
    }

    public async Task<WorkflowDefinition?> RetractAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        var contentItem = await contentManager.GetAsync(definitionId, VersionOptions.Latest);

        if (contentItem == null)
            return null;

        await ownershipGuard.AuthorizeChangeAsync(definitionId, WorkflowChange.Retract);
        // Retracting/Retracted are the content handler's too (UnpublishingAsync, with the
        // definition as unpublished so the trigger indexer drops its triggers).
        await contentManager.UnpublishAsync(contentItem);
        var retracted = Unpublished(contentItem);
        await RefreshCacheAsync(definitionId, cancellationToken);
        return retracted;
    }

    public async Task<WorkflowDefinition> RetractAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        await ownershipGuard.AuthorizeChangeAsync(definition.DefinitionId, WorkflowChange.Retract);
        var contentItem = await contentManager.GetAsync(definition.DefinitionId, VersionOptions.Latest);
        await contentManager.UnpublishAsync(contentItem);
        var retracted = Unpublished(contentItem);
        await RefreshCacheAsync(definition.DefinitionId, cancellationToken);
        return retracted;
    }

    // What the caller gets back: the definition as it is now, unpublished.
    private WorkflowDefinition Unpublished(ContentItem contentItem)
    {
        var definition = workflowDefinitionPartMapper.Map(contentItem.As<WorkflowDefinitionPart>());
        definition.IsPublished = false;
        return definition;
    }

    public async Task<WorkflowDefinition> RevertVersionAsync(string definitionId, int version, CancellationToken cancellationToken = default)
    {
        // Reverting is an edit: a shipped flow forks, a system one refuses.
        var ownership = await ownershipGuard.AuthorizeChangeAsync(definitionId, WorkflowChange.Save);
        var allVersions = (await contentManager.GetAllVersionsAsync(definitionId)).Select(x => x.As<WorkflowDefinitionPart>()).ToList();
        var specifiedVersion = allVersions.FirstOrDefault(x => x.Version == version);
        
        if (specifiedVersion == null)
            throw new ArgumentException($"Workflow definition version {version} not found.");

        var latestVersion = allVersions.OrderByDescending(x => x.Version).First();
        
        // latestVersion.ContentItem.Alter<WorkflowDefinitionPart>(part =>
        // {
        //     part.IsLatest = false;
        // });
        
        var draft = await contentManager.GetAsync(definitionId, VersionOptions.DraftRequired);
        
        draft.Alter<WorkflowDefinitionPart>(part =>
        {
            part.DefinitionVersionId = draft.ContentItemVersionId;
            part.Version = latestVersion.Version + 1;
            part.SerializedData = specifiedVersion.SerializedData;
            part.Description = specifiedVersion.Description;
            part.Name = specifiedVersion.Name;
            // part.IsLatest = true;
            // part.IsPublished = false;
            part.IsReadonly = specifiedVersion.IsReadonly;
            part.IsSystem = specifiedVersion.IsSystem;
            part.MaterializerName = specifiedVersion.MaterializerName;
            part.ProviderName = specifiedVersion.ProviderName;
            part.ToolVersion = specifiedVersion.ToolVersion;
            part.UsableAsActivity = specifiedVersion.UsableAsActivity;
        });

        if (!ownership.IsTenant)
        {
            var model = workflowDefinitionPartMapper.MapModel(draft.As<WorkflowDefinitionPart>());
            model.CustomProperties ??= new Dictionary<string, object>();
            ownership.Stamp(model.CustomProperties);
            draft.Alter<WorkflowDefinitionPart>(part => workflowDefinitionPartMapper.Map(model, part));
        }

        await contentManager.SaveDraftAsync(latestVersion.ContentItem);
        await contentManager.SaveDraftAsync(draft);
        
        await RefreshCacheAsync(definitionId, cancellationToken);
        return workflowDefinitionPartMapper.Map(specifiedVersion);
    }

    public async Task<WorkflowDefinition?> GetDraftAsync(string definitionId, global::Crest.Workflows.Common.Models.VersionOptions versionOptions, CancellationToken cancellationToken = default)
    {
        // Before DraftRequired: Orchard creates (and saves) a new draft version on that read,
        // so a refused edit must be refused here or it leaves a stray draft as the latest
        // version - which for a system flow would read as "no longer published".
        await ownershipGuard.AuthorizeChangeAsync(definitionId, WorkflowChange.Save);
        var contentItem = await contentManager.GetAsync(definitionId, VersionOptions.DraftRequired);

        if (contentItem == null)
            return null;

        contentItem.Alter<WorkflowDefinitionPart>(part =>
        {
            var isNewVersion = part.DefinitionVersionId != contentItem.ContentItemVersionId;
            // part.IsPublished = false;
            // part.IsLatest = true;
            part.DefinitionVersionId = contentItem.ContentItemVersionId;

            if (isNewVersion)
                part.Version++;

            workflowDefinitionPartSerializer.UpdateSerializedData(part);
        });

        return workflowDefinitionPartMapper.Map(contentItem.As<WorkflowDefinitionPart>());
    }

    public async Task<WorkflowDefinition> SaveDraftAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        await ownershipGuard.PrepareAsync(definition, WorkflowChange.Save);
        await mediator.SendAsync(new WorkflowDefinitionDraftSaving(definition), cancellationToken);
        await workflowDefinitionStore.SaveAsync(definition, cancellationToken);
        await mediator.SendAsync(new WorkflowDefinitionDraftSaved(definition), cancellationToken);
        await RefreshCacheAsync(definition.DefinitionId, cancellationToken);
        return definition;
    }
    
    private async Task RefreshCacheAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        await workflowDefinitionCacheManager.EvictWorkflowDefinitionAsync(definitionId, cancellationToken);
        await cacheManager.TriggerTokenAsync(typeof(CachingWorkflowDefinitionStore).FullName!, cancellationToken);
    }
}