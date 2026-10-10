using Microsoft.Extensions.Localization;
using Crest.ContentManagement;
using Crest.Workflows.Platform.Services;

namespace Crest.Contents.Workflows.Activities;

public class ContentUpdatedEvent : ContentEvent
{
    public ContentUpdatedEvent(
        IContentManager contentManager,
        IWorkflowScriptEvaluator scriptEvaluator,
        IStringLocalizer<ContentUpdatedEvent> localizer)
        : base(contentManager, scriptEvaluator, localizer)
    {
    }

    public override string Name => nameof(ContentUpdatedEvent);

    public override LocalizedString DisplayText => S["Content Updated Event"];
}
