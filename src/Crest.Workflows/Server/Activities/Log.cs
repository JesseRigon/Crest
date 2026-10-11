using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Activities;

/// <summary>Writes a line to the tenant's log at the chosen level.</summary>
[Activity("Crest.Workflows", "Primitives", "Writes a message to the application log.", DisplayName = "Log")]
[FlowNode("Done")]
public class Log : Activity
{
    [Input(DisplayName = "Level", Description = "The log level.")]
    public Input<LogLevel> Level { get; set; } = new(LogLevel.Information);

    [Input(DisplayName = "Message", UIHint = InputUIHints.MultiLine)]
    public Input<string?> Message { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var message = Message.GetOrDefault(context) ?? string.Empty;
        context.GetRequiredService<ILogger<Log>>().Log(Level.GetOrDefault(context), "{WorkflowLogMessage}", message);
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}
