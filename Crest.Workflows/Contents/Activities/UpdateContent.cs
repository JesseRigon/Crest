using System.Text.Json.Nodes;
using Crest.Workflows.Extensions;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using JetBrains.Annotations;
using Crest.ContentManagement;
using Crest.ContentManagement.Handlers;
using Crest.Workflows.Contents.UIHints;

namespace Crest.Workflows.Contents.Activities;

[Activity("Crest.Content", "Content", "Update an existing content item.")]
[FlowNode("Updated", "Validation Error")]
[UsedImplicitly]
public class UpdateContent : CodeActivity<ContentItem>
{
    [Input(Description = "The content item ID.")]
    public Input<string> ContentItemId { get; set; } = null!;

    [Input(
        Description = "Whether to publish the content item after creation.",
        UIHint = InputUIHints.Checkbox
    )]
    public Input<bool> Publish { get; set; } = null!;

    [Input(
        Description = "The content properties to set.",
        UIHint = InputUIHints.JsonEditor
    )]
    public Input<JsonObject> ContentProperties { get; set; } = null!;

    [Output(Description = "The validation result of the content item.")] public Output<ContentValidateResult> ValidationResult { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var contentManager = context.GetRequiredService<IContentManager>();
        var contentProperties = ContentProperties.GetOrDefault(context);
        var contentItemId = ContentItemId.Get(context);
        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);

        if (contentProperties != null) 
            contentItem.Merge(contentProperties);

        var result = await contentManager.ValidateAsync(contentItem);
        ValidationResult.Set(context, result);

        if (result.Succeeded)
        {
            var publish = Publish.Get(context);

            if (publish)
                await contentManager.PublishAsync(contentItem);
            else
                await contentManager.SaveDraftAsync(contentItem);

            context.SetResult(contentItem);
            await context.CompleteActivityWithOutcomesAsync("Updated");
            return;
        }

        await context.CompleteActivityWithOutcomesAsync("Validation Error");
    }
}