using Microsoft.Extensions.DependencyInjection;
using Grove.Access;
using Grove.Workflows.Extensions;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Models;
using JetBrains.Annotations;
using Grove.Contents.Management;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Delete a content item.")]
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