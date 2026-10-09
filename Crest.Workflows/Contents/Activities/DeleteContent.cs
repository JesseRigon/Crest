using Crest.Workflows.Extensions;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using JetBrains.Annotations;
using Crest.ContentManagement;

namespace Crest.Workflows.Contents.Activities;

[Activity("Crest.Content", "Content", "Update an existing content item.")]
[FlowNode("Deleted", "Not Found")]
[UsedImplicitly]
public class DeleteContent : CodeActivity<ContentItem>
{
    [Input(Description = "The content item ID.")] public Input<string> ContentItemId { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var contentManager = context.GetRequiredService<IContentManager>();
        var contentItemId = ContentItemId.Get(context);
        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);

        if (contentItem == null)
        {
            await context.CompleteActivityWithOutcomesAsync("Not Found");
            return;
        }

        await contentManager.RemoveAsync(contentItem);

        context.SetResult(contentItem);
        await context.CompleteActivityWithOutcomesAsync("Deleted");
    }
}