using Crest.Workflows.Extensions;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using JetBrains.Annotations;
using Crest.ContentManagement;

namespace Crest.Workflows.Contents.Activities;

[Activity("Crest.Content", "Content", "Unpublish a content item.")]
[FlowNode("Unpublished", "Not Found", "Done")]
[UsedImplicitly]
public class UnpublishContent : CodeActivity<ContentItem>
{
    [Input(Description = "The content item ID.")] public Input<string> ContentItemId { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var contentManager = context.GetRequiredService<IContentManager>();
        var contentItemId = ContentItemId.Get(context);
        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);
        string outcome;

        context.SetResult(contentItem);

        if (contentItem == null)
        {
            outcome = "Not Found";
        }
        else
        {
            outcome = "Unpublished";
            await contentManager.UnpublishAsync(contentItem);
        }

        await context.CompleteActivityWithOutcomesAsync(outcome, "Done");
    }
}