using Crest.Workflows;
using Crest.Workflows.Pipelines.ActivityExecution;
using Crest.Workflows.Runtime.Middleware;
using Crest.Workflows.Runtime.Middleware.Activities;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Extensions;

/// <summary>
/// Adds extensions to <see cref="IActivityExecutionPipelineBuilder"/>.
/// </summary>
public static class ActivityExecutionPipelineBuilderExtensions
{
    /// <summary>
    /// Installs the <see cref="BackgroundActivityInvokerMiddleware"/>.
    /// </summary>
    public static IActivityExecutionPipelineBuilder UseBackgroundActivityInvoker(this IActivityExecutionPipelineBuilder pipelineBuilder) => pipelineBuilder.UseMiddleware<BackgroundActivityInvokerMiddleware>();

    /// <summary>
    /// Installs the <see cref="EvaluateLogPersistenceModesMiddleware"/> which evaluates log persistence modes during activity execution.
    /// </summary>
    public static IActivityExecutionPipelineBuilder UseLogPersistenceModeEvaluation(this IActivityExecutionPipelineBuilder pipelineBuilder) => pipelineBuilder.UseMiddleware<EvaluateLogPersistenceModesMiddleware>();
}