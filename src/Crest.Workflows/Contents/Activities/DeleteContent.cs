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

[Activity("Crest.Content", "Content", "Delete a content item.")]
[FlowNode("Deleted", "Not Found")]
[UsedImplicitly]
public class DeleteContent : CodeActivity<ContentItem>
{
    [Input(Description = "The content item ID.")] public Input<string> ContentItemId { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var contentManager = context.GetRequiredService<IContentManager>();
        var contentItemId = ContentItemId.Get(context);
        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (contentItem == null)
        {
            await context.CompleteActivityWithOutcomesAsync("Not Found");
            return;
        }

        await context.GetRequiredService<IAccessDecision>().RequireAsync(context.GetRequiredService<ICallerContextAccessor>().Current, "DeleteContent", contentItem, context.CancellationToken);

        await contentManager.RemoveAsync(contentItem);

        context.SetResult(contentItem);
        await context.CompleteActivityWithOutcomesAsync("Deleted");
    }
}