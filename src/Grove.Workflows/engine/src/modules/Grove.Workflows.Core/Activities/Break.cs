using System.Runtime.CompilerServices;
using Grove.Workflows.Extensions;
using Grove.Workflows.Attributes;
using Grove.Workflows.Signals;
using JetBrains.Annotations;

namespace Grove.Workflows.Activities;

/// <summary>
/// Break out of a loop.
/// </summary>
[Activity("Grove.Workflows", "Looping", "Break out of a loop.")]
[PublicAPI]
public class Break : CodeActivity, ITerminalNode
{
    /// <inheritdoc />
    public Break([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }

    /// <inheritdoc />
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // Send a signal to the parent scope to break out of the loop.
        await context.SendSignalAsync(new BreakSignal());
    }
}