using System.Runtime.CompilerServices;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Extensions;
using Crest.Workflows;

namespace Crest.Workflows.Scheduling.Activities;

/// <summary>
/// </summary>
public abstract class TimerBase([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : EventGenerator(source, line)
{
    protected abstract TimeSpan GetInterval(ExpressionExecutionContext context);

    protected virtual ValueTask OnTimerElapsedAsync(ActivityExecutionContext context)
    {
        OnTimerElapsed(context);
        return default;
    }

    protected virtual void OnTimerElapsed(ActivityExecutionContext context)
    {
    }
    
    /// <inheritdoc />
    protected override void Execute(ActivityExecutionContext context)
    {
        var interval = GetInterval(context.ExpressionExecutionContext);
        context.RepeatWithInterval(interval, TimerElapsedAsync);
    }

    /// <inheritdoc />
    protected override object GetTriggerPayload(TriggerIndexingContext context)
    {
        var interval = GetInterval(context.ExpressionExecutionContext);
        return context.GetTimerTriggerStimulus(interval);
    }
    
    private async ValueTask TimerElapsedAsync(ActivityExecutionContext context)
    {
        await OnTimerElapsedAsync(context);
        await context.CompleteActivityAsync();
    }
}