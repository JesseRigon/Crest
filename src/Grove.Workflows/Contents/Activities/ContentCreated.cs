using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item is created.")]
public class ContentCreated : ContentEventTriggerBase;