using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item has been cloned, once the clone commits.")]
public class ContentCloned : ContentEventTriggerBase;
