using Microsoft.Extensions.Localization;
using Crest.ContentManagement;
using Crest.ContentManagement.Workflows;
using Crest.Workflows.Platform.Abstractions.Models;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Services;

namespace Crest.Contents.Workflows.Activities;

public class RetrieveContentTask : ContentTask
{
    public RetrieveContentTask(
        IContentManager contentManager,
        IWorkflowScriptEvaluator scriptEvaluator,
        IStringLocalizer<RetrieveContentTask> localizer)
        : base(contentManager, scriptEvaluator, localizer)
    {
    }

    public override string Name => nameof(RetrieveContentTask);

    public override LocalizedString DisplayText => S["Retrieve Content Task"];

    public override LocalizedString Category => S["Content"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(S["Retrieved"]);

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var contentItemId = (await GetContentItemIdAsync(workflowContext))
            ?? throw new InvalidOperationException($"The '{nameof(RetrieveContentTask)}' failed to evaluate the 'ContentItemId'.");

        var contentItem = (await ContentManager.GetAsync(contentItemId, VersionOptions.Latest))
            ?? throw new InvalidOperationException($"The '{nameof(RetrieveContentTask)}' failed to retrieve the content item.");

        if (string.IsNullOrEmpty(workflowContext.CorrelationId))
        {
            workflowContext.CorrelationId = contentItem.ContentItemId;
        }

        workflowContext.Properties[ContentEventConstants.ContentItemInputKey] = contentItem;
        workflowContext.LastResult = contentItem;

        return Outcome("Retrieved");
    }
}
