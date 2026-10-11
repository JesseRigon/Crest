using Microsoft.Extensions.DependencyInjection;
using Crest.Access;
using Crest.Workflows.Extensions;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using JetBrains.Annotations;
using Crest.ContentManagement;

namespace Crest.Workflows.Contents.Activities;

[Activity("Crest.Content", "Content", "Publish a content item.")]
[FlowNode("Published", "Already Published", "Not Found", "Done")]
[UsedImplicitly]
public class PublishContent : CodeActivity<ContentItem>
{
    [Input(Description = "The content item ID.")] public Input<string> ContentItemId { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var contentManager = context.GetRequiredService<IContentManager>();
        var contentItemId = ContentItemId.Get(context);
        // Latest, not DraftRequired: asking for a draft would make one, and every item would
        // look unpublished.
        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
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
            await context.GetRequiredService<IAccessDecision>().RequireAsync(context.GetRequiredService<ICallerContextAccessor>().Current, "PublishContent", contentItem, context.CancellationToken);
            outcome = "Published";
            await contentManager.PublishAsync(contentItem);
        }

        await context.CompleteActivityWithOutcomesAsync(outcome, "Done");
    }
}