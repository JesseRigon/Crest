using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Extensions;
using Crest.Workflows;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.Workflows.Contents.Stimuli;
using Crest.Workflows.Contents.UIHints;
using Crest.Workflows.Contexts;

namespace Crest.Workflows.Contents.Activities;

/// <summary>
/// Base of the content triggers. Matches on content type (the stimulus), then, when
/// <see cref="RequiredPermission"/> is set, on the acting user: a user who lacks the
/// permission ends the run on the "Denied" port without a result, so a flow can be
/// restricted to the people whose actions may cause its effects. The check runs through
/// Crest's authorization with the snapshotted principal (member ceiling included).
/// </summary>
public abstract class ContentEventTriggerBase : Trigger<ContentItem>
{
    [Input(
        DisplayName = "Content Types",
        Description = "The content types to trigger on.",
        UIHint = InputUIHints.CheckList,
        UIHandler = typeof(ContentTypeCheckListOptionsProvider)
    )]
    public Input<ICollection<string>> ContentTypes { get; set; } = null!;

    [Input(
        DisplayName = "Required permission",
        Description = "Optional. Crest permission the acting user must hold for the flow to proceed; otherwise the run ends on Denied.",
        UIHint = InputUIHints.SingleLine
    )]
    public Input<string?> RequiredPermission { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        if (context.IsTriggerOfWorkflow())
            await ExecuteInternalAsync(context);
        else
            context.CreateBookmarks(GetExpectedStimuli(context.ExpressionExecutionContext), ExecuteInternalAsync, false);
    }

    protected override ValueTask<IEnumerable<object>> GetTriggerPayloadsAsync(TriggerIndexingContext context)
    {
        return new(GetExpectedStimuli(context.ExpressionExecutionContext));
    }

    private IEnumerable<object> GetExpectedStimuli(ExpressionExecutionContext context)
    {
        var contentTypes = ContentTypes.GetOrDefault(context) ?? [];
        return contentTypes.Select(x => new ContentEventStimulus(x)).ToList();
    }

    private async ValueTask ExecuteInternalAsync(ActivityExecutionContext context)
    {
        var requiredPermission = RequiredPermission.GetOrDefault(context);
        if (!string.IsNullOrWhiteSpace(requiredPermission))
        {
            var user = context.FindWorkflowInput<WorkflowUserContext>(WorkflowUserContext.InputKey);
            var authorizer = context.GetRequiredService<IWorkflowAuthorizer>();
            if (!await authorizer.AuthorizeAsync(user, requiredPermission, context.CancellationToken))
            {
                await context.CompleteActivityWithOutcomesAsync("Denied");
                return;
            }
        }

        var contentItem = context.GetWorkflowInput<ContentItem>();
        context.SetResult(contentItem);
        await context.CompleteActivityAsync();
    }
}
