using Microsoft.Extensions.Localization;
using Crest.ContentManagement;
using Crest.Workflows.Platform.Services;

namespace Crest.Contents.Workflows.Activities;

public class ContentDraftSavedEvent : ContentEvent
{
    public ContentDraftSavedEvent(
        IContentManager contentManager,
        IWorkflowScriptEvaluator scriptEvaluator,
        IStringLocalizer<ContentDraftSavedEvent> localizer)
        : base(contentManager, scriptEvaluator, localizer)
    {
    }

    public override string Name => nameof(ContentDraftSavedEvent);

    public override LocalizedString DisplayText => S["Content Save Draft Event"];
}
