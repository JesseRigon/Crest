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

[Activity("Crest.Content", "Content", "Get an existing content item.")]
[FlowNode("Found", "Not Found")]
[UsedImplicitly]
public class GetContent : CodeActivity<ContentItem>
{
    [Input(Description = "The content item ID.")]
    public Input<string> ContentItemId { get; set; } = null!;
    
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var contentManager = context.GetRequiredService<IContentManager>();
        var contentItemId = ContentItemId.Get(context);
        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        // An item the caller may not view is not found: a read never reveals what it hides.
        if (contentItem != null && !await context.GetRequiredService<IAccessDecision>().AllowsAsync(context.GetRequiredService<ICallerContextAccessor>().Current, "ViewContent", contentItem, context.CancellationToken))
        {
            contentItem = null;
        }

        var outcome = contentItem != null ? "Found" : "Not Found";
        context.SetResult(contentItem);
        await context.CompleteActivityWithOutcomesAsync(outcome);
    }
}