using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item draft has been published.")]
public class ContentPublished : ContentEventTriggerBase;