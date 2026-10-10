using System.Runtime.CompilerServices;
using Crest.Workflows.Extensions;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using JetBrains.Annotations;

namespace Crest.Workflows.Activities.Flowchart.Activities;

/// <summary>
/// Branch execution into multiple branches that will be executed in parallel.
/// </summary>
[Activity("Crest.Workflows", "Branching", "Branch execution into multiple branches that will be executed in parallel.", DisplayName = "Fork (flow)")]
[PublicAPI]
public class FlowFork : Activity
{
    /// <inheritdoc />
    public FlowFork([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }

    /// <summary>
    /// A list of expected outcomes to handle.
    /// </summary>
    [Input(
        Description = "A list of expected outcomes to handle.",
        UIHint = InputUIHints.DynamicOutcomes
    )]
    public Input<ICollection<string>> Branches { get; set; } = null!;

    /// <inheritdoc />
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var outcomes = Branches.GetOrDefault(context)?.ToArray() ?? ["Done"];

        await context.CompleteActivityWithOutcomesAsync(outcomes);
    }
}
