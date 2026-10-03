using System.Runtime.CompilerServices;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Extensions;
using Crest.Workflows.Attributes;
using Crest.Workflows.Behaviors;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using JetBrains.Annotations;

namespace Crest.Workflows.Activities;

/// <summary>
/// Execute an activity while a given condition evaluates to true.
/// </summary>
[Activity("Crest.Workflows", "Looping", "Execute an activity while a given condition evaluates to true.")]
[PublicAPI]
public class While : Activity
{
    /// <summary>
    /// Creates a <see cref="While"/> activity that loops forever.
    /// </summary>
    public static While True(IActivity body) => new(body)
    {
        Condition = new(true)
    };

    /// <inheritdoc />
    public While([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
        Behaviors.Add<BreakBehavior>(this);
    }

    /// <inheritdoc />
    public While(IActivity? body = null, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : this(source, line)
    {
        Body = body;
    }

    /// <inheritdoc />
    public While(Input<bool> condition, IActivity? body = null, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : this(body, source, line)
    {
        Condition = condition;
    }

    /// <inheritdoc />
    public While(Func<ExpressionExecutionContext, ValueTask<bool>> condition, IActivity? body = null, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null)
        : this(new Input<bool>(condition), body, source, line)
    {
    }

    /// <inheritdoc />
    public While(Func<ExpressionExecutionContext, bool> condition, IActivity? body = null, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null)
        : this(new Input<bool>(condition), body, source, line)
    {
    }

    /// <inheritdoc />
    public While(Func<ValueTask<bool>> condition, IActivity? body = null, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null)
        : this(new Input<bool>(condition), body, source, line)
    {
    }

    /// <inheritdoc />
    public While(Func<bool> condition, IActivity? body = null, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null)
        : this(new Input<bool>(condition), body, source, line)
    {
    }

    /// <summary>
    /// The condition to evaluate.
    /// </summary>
    [Input(AutoEvaluate = false, UIHint = InputUIHints.SingleLine)]
    public Input<bool> Condition { get; set; } = new(false);

    /// <summary>
    /// The <see cref="IActivity"/> to execute on every iteration.
    /// </summary>
    [Port]
    public IActivity? Body { get; set; }

    /// <inheritdoc />
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context) => await HandleIterationAsync(context);

    private async ValueTask OnBodyCompleted(ActivityCompletedContext context)
    {
        await HandleIterationAsync(context.TargetContext);
    }

    private async ValueTask HandleIterationAsync(ActivityExecutionContext context)
    {
        var isBreaking = context.GetIsBreaking();
        var loop = !isBreaking && await context.EvaluateInputPropertyAsync<While, bool>(x => x.Condition);

        if (loop)
            await context.ScheduleActivityAsync(Body, OnBodyCompleted);
        else
            await context.CompleteActivityAsync();
    }
}