using Microsoft.Extensions.Localization;
using Crest.ContentManagement;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Services;

namespace Crest.Contents.Workflows.Activities;

public abstract class ContentTask : ContentActivity, ITask
{
    protected ContentTask(
        IContentManager contentManager,
        IWorkflowScriptEvaluator scriptEvaluator,
        IStringLocalizer localizer)
        : base(contentManager, scriptEvaluator, localizer)
    {
    }
}
