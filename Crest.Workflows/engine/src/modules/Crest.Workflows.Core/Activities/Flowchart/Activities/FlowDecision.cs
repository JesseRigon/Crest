using System.Runtime.CompilerServices;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using JetBrains.Annotations;

namespace Crest.Workflows.Activities.Flowchart.Activities;

/// <summary>
/// Performs a boolean condition and returns an outcome based on the result.
/// </summary>
[FlowNode("True", "False")]
[Activity("Crest.Workflows", "Branching", "Evaluate a Boolean condition to determine which path to execute next.", DisplayName = "Decision")]
[PublicAPI]
public class FlowDecision : Activity
{
    /// <inheritdoc />
    public FlowDecision([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }

    /// <inheritdoc />
    public FlowDecision(Func<ExpressionExecutionContext, bool> condition, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : this(source, line)
    {
        Condition = new(condition);
    }
    
    /// <inheritdoc />
    public FlowDecision(Func<ExpressionExecutionContext, ValueTask<bool>> condition, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : this(source, line)
    {
        Condition = new(condition);
    }
    
    /// <summary>
    /// The condition to evaluate.
    /// </summary>
    [Input(UIHint = InputUIHints.SingleLine)]
    public Input<bool> Condition { get; set; } = new(new Literal<bool>(false));

    /// <inheritdoc />
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var result = context.Get(Condition);
        var outcome = result ? "True" : "False";

        await context.CompleteActivityWithOutcomesAsync(outcome);
    }
}