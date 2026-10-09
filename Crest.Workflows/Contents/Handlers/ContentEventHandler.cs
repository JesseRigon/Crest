using Crest.Workflows;
using Crest.Workflows.Runtime;
using OrchardCore.ContentManagement.Handlers;
using Crest.Workflows.Contents.Activities;
using Crest.Workflows.Contents.Stimuli;
using Crest.Workflows.Contexts;

namespace Crest.Workflows.Contents.Handlers;

/// <summary>
/// Turns Orchard content events into engine stimuli, queued to fire after commit. Every
/// stimulus carries the acting user snapshot (<see cref="WorkflowUserContext.InputKey"/>)
/// so triggers and the RequirePermission activity can decide with the real principal, and
/// the content item id as correlation id so one item's flows can be found together.
/// </summary>
public class ContentEventHandler(Units.WorkflowStimulusQueue queue, IWorkflowUserContextAccessor userContext) : ContentHandlerBase
{
    public override Task CreatedAsync(CreateContentContext context) => TriggerActivity<ContentCreated>(context);
    public override Task DraftSavedAsync(SaveDraftContentContext context) => TriggerActivity<ContentDraftSaved>(context);
    public override Task RemovedAsync(RemoveContentContext context) => TriggerActivity<ContentDeleted>(context);
    public override Task PublishedAsync(PublishContentContext context) => TriggerActivity<ContentPublished>(context);
    public override Task UnpublishedAsync(PublishContentContext context) => TriggerActivity<ContentUnpublished>(context);
    public override Task UpdatedAsync(UpdateContentContext context) => TriggerActivity<ContentUpdated>(context);
    public override Task VersionedAsync(VersionContentContext context) => TriggerActivity<ContentVersioned>(context);

    private async Task TriggerActivity<TTriggerActivity>(ContentContextBase context) where TTriggerActivity : IActivity
    {
        var contentItem = context.ContentItem;

        // Workflow definitions are content items too; publishing one must not fan out
        // to content triggers.
        if (contentItem.ContentType == "WorkflowDefinition")
        {
            return;
        }

        var stimulus = new ContentEventStimulus(contentItem.ContentType);

        // Fires after the current unit commits: a flow never sees an item whose write was
        // rolled back, and content written inside a workflow burst reaches its listeners only
        // once that burst is in (docs/workflows.md › Posting on workflows).
        await queue.EnqueueAsync<TTriggerActivity>(stimulus, new()
        {
            CorrelationId = contentItem.ContentItemId,
            Input = new Dictionary<string, object>
            {
                ["ContentItem"] = contentItem,
                [WorkflowUserContext.InputKey] = userContext.Capture(),
            },
        });
    }
}
