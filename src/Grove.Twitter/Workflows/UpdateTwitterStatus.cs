using Grove.Twitter.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;
using Grove.Workflows.UIHints;
using Microsoft.Extensions.Logging;

namespace Grove.Twitter.Workflows;

/// <summary>
/// Posts a status through the tenant's X (Twitter) client. An external effect: runs after the
/// unit that reached it commits.
/// </summary>
[Activity("Grove.Twitter", "Social", "Posts a status on X (Twitter) after this flow's transaction commits.", DisplayName = "Update X status", Kind = ActivityKind.Task, RunAsynchronously = true)]
[FlowNode("Done", "Failed")]
public class UpdateTwitterStatus : Activity, IUnitBoundary
{
    [Input(DisplayName = "Status", Description = "The text to post.", UIHint = InputUIHints.MultiLine)]
    public Input<string> Status { get; set; } = null!;

    [Output(Description = "The API's response body.")]
    public Output<string?> Response { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var status = Status.GetOrDefault(context);
        if (string.IsNullOrWhiteSpace(status))
        {
            await FailAsync(context, "No status text.");
            return;
        }

        var response = await context.GetRequiredService<TwitterClient>().UpdateStatus(status);
        var body = await response.Content.ReadAsStringAsync();
        Response.Set(context, body);

        if (!response.IsSuccessStatusCode)
        {
            await FailAsync(context, $"X answered {(int)response.StatusCode}.");
            return;
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    private async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.GetRequiredService<ILogger<UpdateTwitterStatus>>().LogWarning("Update X status failed: {Reason}", reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>What Twitter registers with the workflow registry.</summary>
public sealed class TwitterWorkflowProvider : IWorkflowActivityProvider
{
    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("twitter.update-status", "Update X status", WorkflowsConstants.Objects.Platform, "Grove.Twitter.UpdateTwitterStatus", "Posts a status on X (Twitter) after the unit commits.", 10, Category: "Social"),
    ];
}
