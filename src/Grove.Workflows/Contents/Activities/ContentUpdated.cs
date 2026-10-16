using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item has been updated, once the update commits.")]
public class ContentUpdated : ContentEventTriggerBase;
