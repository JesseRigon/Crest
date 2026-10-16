using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item has been unpublished, once the change commits.")]
public class ContentUnpublished : ContentEventTriggerBase;
