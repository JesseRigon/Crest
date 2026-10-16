using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item has been imported, once the import commits.")]
public class ContentImported : ContentEventTriggerBase;
