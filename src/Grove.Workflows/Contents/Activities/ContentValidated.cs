using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item has passed validation, once the unit commits.")]
public class ContentValidated : ContentEventTriggerBase;
