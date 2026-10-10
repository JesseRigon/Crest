using Crest.Workflows;
using Crest.Workflows.Features;
using Crest.Workflows.Middleware.Activities;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Extensions;

/// <summary>
/// Adds an extension method to the <see cref="WorkflowsFeature"/> that installs a default workflow runtime execution pipeline.
/// </summary>
public static class PipelineWorkflowsFeatureExtensions
{
    /// <summary>
    /// Installs a default workflow runtime execution pipeline.
    /// </summary>
    public static WorkflowsFeature WithDefaultWorkflowExecutionPipeline(this WorkflowsFeature workflowsFeature, Action<IWorkflowExecutionPipelineBuilder>? configurePipeline = null)
    {
        return workflowsFeature.WithWorkflowExecutionPipeline(pipeline =>
        {
            pipeline.UseDefaultPipeline();
            configurePipeline?.Invoke(pipeline);
        });
    }

    /// <summary>
    /// Installs an activity invoker that can run activities asynchronously in the background.
    /// </summary>
    public static WorkflowsFeature WithDefaultActivityExecutionPipeline(this WorkflowsFeature workflowsFeature, Action<IActivityExecutionPipelineBuilder>? configurePipeline = null)
    {
        return workflowsFeature.WithActivityExecutionPipeline(pipeline =>
        {
            pipeline
                .UseExceptionHandling()
                .UseExecutionLogging()
                .UseNotifications()
                .UseLogPersistenceModeEvaluation()
                .UseBackgroundActivityInvoker();

            configurePipeline?.Invoke(pipeline);
        });
    }
}