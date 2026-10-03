using Crest.Workflows;

namespace Crest.Workflows.Expressions.Liquid.Helpers;

/// <summary>
/// A model that is passed to Liquid templates when rendering an activity.
/// </summary>
public record LiquidActivityModel(ActivityExecutionContext ActivityExecutionContext, string? ActivityName, string? ActivityId)
{
}