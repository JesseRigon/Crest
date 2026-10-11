using Crest.Facebook.Models;
using Crest.Facebook.Services;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.Logging;

namespace Crest.Facebook.Workflows;

/// <summary>
/// Sends a server-side event to the Meta Conversions API (a completed order, a captured
/// lead) independently of, and optionally de-duplicated with, the browser-side Meta Pixel.
/// An external effect: runs after the unit that reached it commits.
/// </summary>
[Activity("Crest.Facebook", "Meta", "Sends a server-side event to the Meta Conversions API after this flow's transaction commits.", DisplayName = "Send Meta conversion event", Kind = ActivityKind.Task, RunAsynchronously = true)]
[FlowNode("Done", "Failed")]
public class SendMetaConversionEvent : Activity, IUnitBoundary
{
    [Input(DisplayName = "Event name", Description = "The Meta standard or custom event name, e.g. Purchase or Lead.", UIHint = InputUIHints.SingleLine)]
    public Input<string> EventName { get; set; } = null!;

    [Input(DisplayName = "Event source URL", Description = "The page the event happened on, when it has one.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> EventSourceUrl { get; set; } = null!;

    [Input(DisplayName = "Action source", Description = "Where the conversion happened.")]
    public Input<MetaActionSource> ActionSource { get; set; } = new(MetaActionSource.Website);

    [Input(DisplayName = "Event id", Description = "An id shared with the Pixel event so Meta de-duplicates the two.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> EventId { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var eventName = EventName.GetOrDefault(context)?.Trim();
        if (string.IsNullOrEmpty(eventName))
        {
            await FailAsync(context, "No event name.");
            return;
        }

        var eventSourceUrl = EventSourceUrl.GetOrDefault(context);
        var eventId = EventId.GetOrDefault(context);
        var result = await context.GetRequiredService<IMetaConversionsApiService>().SendEventAsync(new MetaConversionEvent
        {
            EventName = eventName,
            EventSourceUrl = string.IsNullOrWhiteSpace(eventSourceUrl) ? null : eventSourceUrl,
            ActionSource = ActionSource.GetOrDefault(context),
            EventId = string.IsNullOrWhiteSpace(eventId) ? null : eventId,
        });

        if (!result.Succeeded)
        {
            await FailAsync(context, string.Join("; ", result.Errors.Select(error => error.Message?.ToString())));
            return;
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    private async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.GetRequiredService<ILogger<SendMetaConversionEvent>>().LogWarning("Send Meta conversion event failed: {Reason}", reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>What the Meta Pixel feature registers with the workflow registry.</summary>
public sealed class FacebookWorkflowProvider : IWorkflowActivityProvider
{
    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("meta.send-conversion-event", "Send Meta conversion event", WorkflowsConstants.Objects.Platform, "Crest.Facebook.SendMetaConversionEvent", "Sends a server-side event to the Meta Conversions API after the unit commits.", 10, Category: "Meta"),
    ];
}
