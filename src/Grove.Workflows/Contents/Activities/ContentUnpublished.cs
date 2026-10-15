using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item draft has been unpublished.")]
public class ContentUnpublished : ContentEventTriggerBase;