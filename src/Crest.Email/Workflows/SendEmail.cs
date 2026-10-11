using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.Logging;

namespace Crest.Email.Workflows;

/// <summary>
/// Sends an e-mail through the tenant's e-mail service. An external effect: the unit that
/// reached this node commits first, the mail goes out in a unit of its own, and the flow
/// resumes on Done or Failed with the provider's reason.
/// </summary>
[Activity("Crest.Email", "Messaging", "Sends an e-mail through the tenant's e-mail provider after this flow's transaction commits.", DisplayName = "Send email", Kind = ActivityKind.Task, RunAsynchronously = true)]
[FlowNode("Done", "Failed")]
public class SendEmail : Activity, IUnitBoundary
{
    [Input(DisplayName = "To", Description = "Recipient addresses, comma separated.", UIHint = InputUIHints.SingleLine)]
    public Input<string> To { get; set; } = null!;

    [Input(DisplayName = "Cc", Description = "Carbon-copy addresses, comma separated.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Cc { get; set; } = null!;

    [Input(DisplayName = "Bcc", Description = "Blind-copy addresses, comma separated.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Bcc { get; set; } = null!;

    [Input(DisplayName = "From", Description = "The author shown as the sender. Empty = the provider's default.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> From { get; set; } = null!;

    [Input(DisplayName = "Sender", Description = "The envelope sender when it differs from the author.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Sender { get; set; } = null!;

    [Input(DisplayName = "Reply to", Description = "The reply-to address.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> ReplyTo { get; set; } = null!;

    [Input(DisplayName = "Subject", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Subject { get; set; } = null!;

    [Input(DisplayName = "Text body", Description = "The plain-text body.", UIHint = InputUIHints.MultiLine)]
    public Input<string?> TextBody { get; set; } = null!;

    [Input(DisplayName = "HTML body", Description = "The HTML body.", UIHint = InputUIHints.MultiLine)]
    public Input<string?> HtmlBody { get; set; } = null!;

    [Input(DisplayName = "Body format", Description = "Which bodies to send: both, text only or HTML only.")]
    public Input<MailMessageBodyFormat> BodyFormat { get; set; } = new(MailMessageBodyFormat.All);

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var to = To.GetOrDefault(context)?.Trim();
        if (string.IsNullOrEmpty(to))
        {
            await FailAsync(context, "No recipient.");
            return;
        }

        var format = BodyFormat.GetOrDefault(context);
        var message = new MailMessage
        {
            From = From.GetOrDefault(context)?.Trim() ?? Sender.GetOrDefault(context)?.Trim(),
            To = to,
            Cc = Cc.GetOrDefault(context)?.Trim(),
            Bcc = Bcc.GetOrDefault(context)?.Trim(),
            ReplyTo = ReplyTo.GetOrDefault(context)?.Trim(),
            Subject = Subject.GetOrDefault(context)?.Trim(),
            TextBody = format is MailMessageBodyFormat.All or MailMessageBodyFormat.Text ? TextBody.GetOrDefault(context)?.Trim() : null,
            HtmlBody = format is MailMessageBodyFormat.All or MailMessageBodyFormat.Html ? HtmlBody.GetOrDefault(context)?.Trim() : null,
        };

        var sender = Sender.GetOrDefault(context)?.Trim();
        if (!string.IsNullOrEmpty(sender))
        {
            message.Sender = sender;
        }

        var result = await context.GetRequiredService<IEmailService>().SendAsync(message);
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
        context.GetRequiredService<ILogger<SendEmail>>().LogWarning("Send email failed: {Reason}", reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>What Email registers with the workflow registry.</summary>
public sealed class EmailWorkflowProvider : IWorkflowActivityProvider
{
    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("email.send", "Send email", WorkflowsConstants.Objects.Platform, "Crest.Email.SendEmail", "Sends an e-mail through the tenant's e-mail provider after the unit commits.", 10, Category: "Messaging"),
    ];
}
