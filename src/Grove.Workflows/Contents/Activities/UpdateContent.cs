using Microsoft.Extensions.DependencyInjection;
using Grove.Access;
using System.Text.Json.Nodes;
using Grove.Workflows.Extensions;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Models;
using Grove.Workflows.UIHints;
using JetBrains.Annotations;
using Grove.ContentManagement;
using Grove.ContentManagement.Handlers;
using Grove.Workflows.Contents.UIHints;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Update an existing content item.")]
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
        // Decided against the item as it stands, before a draft is made for the change.
        var current = await contentManager.GetAsync(contentItemId, VersionOptions.Latest)
            ?? throw new InvalidOperationException($"Content item '{contentItemId}' was not found.");
        var decision = context.GetRequiredService<IAccessDecision>();
        await decision.RequireAsync(context.GetRequiredService<ICallerContextAccessor>().Current, "EditContent", current, context.CancellationToken);
        if (Publish.Get(context))
        {
            await decision.RequireAsync(context.GetRequiredService<ICallerContextAccessor>().Current, "PublishContent", current, context.CancellationToken);
        }

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