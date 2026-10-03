using Crest.Workflows.Extensions;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using JetBrains.Annotations;
using OrchardCore.ContentManagement;

namespace Crest.Workflows.Contents.Activities;

[Activity("OrchardCore.Content", "Content", "Get an existing content item.")]
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
        var outcome = contentItem != null ? "Found" : "Not Found";
        context.SetResult(contentItem);
        await context.CompleteActivityWithOutcomesAsync(outcome);
    }
}