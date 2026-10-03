using System.ComponentModel;
using System.Runtime.CompilerServices;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Attributes;
using Crest.Workflows.Memory;
using Crest.Workflows.Models;
using JetBrains.Annotations;

namespace Crest.Workflows.Activities;

/// <summary>
/// Set the CorrelationId of the workflow to a given value.
/// </summary>
[Activity("Crest.Workflows", "Primitives", "Set the CorrelationId of the workflow to a given value.")]
[PublicAPI]
public class Correlate : CodeActivity
{
    /// <inheritdoc />
    public Correlate([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }

    /// <inheritdoc />
    public Correlate(string correlationId, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
        CorrelationId = new(correlationId);
    }

    /// <inheritdoc />
    public Correlate(Variable<string> correlationId, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
        CorrelationId = new(correlationId);
    }

    /// <inheritdoc />
    public Correlate(Func<ExpressionExecutionContext, string> correlationId, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
        CorrelationId = new(correlationId);
    }

    /// <summary>
    /// The correlation ID to set.
    /// </summary>
    [Description("An expression that evaluates to the value to store as the correlation id")]
    public Input<string> CorrelationId { get; set; } = null!;

    /// <inheritdoc />
    protected override void Execute(ActivityExecutionContext context)
    {
        var correlationId = context.Get(CorrelationId);
        context.WorkflowExecutionContext.CorrelationId = correlationId;
    }
}