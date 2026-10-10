using Microsoft.Extensions.Localization;
using Crest.ContentManagement;
using Crest.Workflows.Platform.Services;

namespace Crest.Contents.Workflows.Activities;

public class ContentVersionedEvent : ContentEvent
{
    public ContentVersionedEvent(IContentManager contentManager,
        IWorkflowScriptEvaluator scriptEvaluator,
        IStringLocalizer<ContentVersionedEvent> localizer)
        : base(contentManager, scriptEvaluator, localizer)
    {
    }

    public override string Name => nameof(ContentVersionedEvent);

    public override LocalizedString DisplayText => S["Content Versioned Event"];
}
