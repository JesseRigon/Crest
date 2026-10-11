using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.Logging;

namespace Crest.Sms.Workflows;

/// <summary>
/// Sends a text message through the tenant's SMS service. An external effect: the unit that
/// reached this node commits first and the message goes out in a unit of its own.
/// </summary>
[Activity("Crest.Sms", "Messaging", "Sends an SMS through the tenant's SMS provider after this flow's transaction commits.", DisplayName = "Send SMS", Kind = ActivityKind.Task, RunAsynchronously = true)]
[FlowNode("Done", "Failed")]
public class SendSms : Activity, IUnitBoundary
{
    [Input(DisplayName = "Phone number", Description = "The recipient's number, in international format.", UIHint = InputUIHints.SingleLine)]
    public Input<string> PhoneNumber { get; set; } = null!;

    [Input(DisplayName = "Body", UIHint = InputUIHints.MultiLine)]
    public Input<string> Body { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var phoneNumber = PhoneNumber.GetOrDefault(context)?.Trim();
        if (string.IsNullOrEmpty(phoneNumber))
        {
            await FailAsync(context, "No phone number.");
            return;
        }

        var result = await context.GetRequiredService<ISmsService>().SendAsync(phoneNumber, Body.GetOrDefault(context) ?? string.Empty);
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
        context.GetRequiredService<ILogger<SendSms>>().LogWarning("Send SMS failed: {Reason}", reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>What Sms registers with the workflow registry.</summary>
public sealed class SmsWorkflowProvider : IWorkflowActivityProvider
{
    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("sms.send", "Send SMS", WorkflowsConstants.Objects.Platform, "Crest.Sms.SendSms", "Sends an SMS through the tenant's SMS provider after the unit commits.", 20, Category: "Messaging"),
    ];
}
