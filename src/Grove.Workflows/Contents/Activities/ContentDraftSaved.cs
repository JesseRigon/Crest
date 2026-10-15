using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item draft has been saved.")]
public class ContentDraftSaved : ContentEventTriggerBase;