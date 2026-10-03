using Crest.Workflows.Extensions;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using JetBrains.Annotations;
using OrchardCore.ContentManagement;

namespace Crest.Workflows.Contents.Activities;

[Activity("OrchardCore.Content", "Content", "Publish a content item.")]
[FlowNode("Published", "Already Published", "Not Found", "Done")]
[UsedImplicitly]
public class PublishContent : CodeActivity<ContentItem>
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
        else if (!contentItem.HasDraft())
        {
            outcome = "Already Published";
        }
        else
        {
            outcome = "Published";
            await contentManager.PublishAsync(contentItem);
        }

        await context.CompleteActivityWithOutcomesAsync(outcome, "Done");
    }
}