#nullable enable

using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Data.Connectors;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;
using Grove.Workflows.UIHints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grove.Integrations.Twitter.Workflows;

/// <summary>
/// Posts on X through the X connection (<c>createPost</c>), as the account the tenant connected
/// on the Connections screen. An external effect: runs after the unit that reached it commits.
/// The engine makes no retry of its own: the post is not idempotent.
/// </summary>
[Activity("Grove.Integrations.Twitter", "Social", "Posts on X (Twitter) through the X connection after this flow's transaction commits.", DisplayName = "Update X status", Kind = ActivityKind.Task, RunAsynchronously = true)]
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

        var engine = context.GetService<IConnectorEngine>();
        if (engine is null)
        {
            await FailAsync(context, "The connector engine is not enabled.");
            return;
        }

        var result = await engine.ExecuteAsync(new ConnectorCall
        {
            ConnectionKey = TwitterConstants.ConnectionKey,
            Caller = await ResolveCallerAsync(context),
            OperationId = TwitterConstants.Operations.CreatePost,
            Arguments = new JsonObject { ["text"] = status },
            Scope = CredentialScope.Tenant,
            MaxRetries = 0,
        }, context.CancellationToken);

        Response.Set(context, result.Body);

        if (!result.Succeeded)
        {
            await FailAsync(context, result.Outcome switch
            {
                ConnectorOutcome.NotAuthorized => "The X connection has no account connected (Connections).",
                ConnectorOutcome.RetryAfter => $"X asked to wait until {result.RetryAt:u}.",
                _ => result.Error ?? $"X answered {result.StatusCode}.",
            });
            return;
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    /// <summary>The flow's caller when it may use connectors, else the tenant's system actor.</summary>
    internal static async Task<CallerContext> ResolveCallerAsync(ActivityExecutionContext context)
    {
        var current = context.GetService<ICallerContextAccessor>()?.Current;
        if (current is not null && ConnectorCallers.MayCall(current))
        {
            return current;
        }

        return await context.GetRequiredService<ICallerContextFactory>().CreateSystemAsync(cancellationToken: context.CancellationToken);
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
        new("twitter.update-status", "Update X status", WorkflowsConstants.Objects.Platform, "Grove.Integrations.Twitter.UpdateTwitterStatus", "Posts on X through the X connection after the unit commits.", 10, Category: "Social"),
    ];
}
