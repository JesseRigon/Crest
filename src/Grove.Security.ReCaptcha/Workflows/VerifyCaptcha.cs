using Grove.Security.ReCaptcha.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;
using Grove.Workflows.UIHints;

namespace Grove.Security.ReCaptcha.Workflows;

/// <summary>
/// Verifies a captcha token a form sent (docs/widgets.md, the captcha): the <c>Captcha</c>
/// component adds its token to the form's arguments, the UI action passes it in, and this checks
/// it with the same ReCaptcha service login and registration use. Ends on Valid or Invalid.
/// </summary>
[Activity("Grove.Security.ReCaptcha", "Security", "Verifies a ReCaptcha token a form submitted.", DisplayName = "Verify captcha")]
[FlowNode("Valid", "Invalid")]
public class VerifyCaptcha : Activity
{
    [Input(DisplayName = "Token", Description = "The token the form's Captcha component added (by default its 'captcha' argument).", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Token { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var token = Token.GetOrDefault(context);
        var valid = !string.IsNullOrWhiteSpace(token)
            && await context.GetRequiredService<ReCaptchaService>().VerifyCaptchaResponseAsync(token);

        await context.CompleteActivityWithOutcomesAsync(valid ? "Valid" : "Invalid");
    }
}

/// <summary>What the ReCaptcha feature registers with the workflow registry.</summary>
public sealed class ReCaptchaWorkflowProvider : IWorkflowActivityProvider
{
    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("recaptcha.verify", "Verify captcha", WorkflowsConstants.Objects.Platform, "Grove.Security.ReCaptcha.VerifyCaptcha", "Verifies a ReCaptcha token a form submitted.", 10, Category: "Security"),
    ];
}
