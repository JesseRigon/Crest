using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item has been deleted.")]
public class ContentDeleted : ContentEventTriggerBase;