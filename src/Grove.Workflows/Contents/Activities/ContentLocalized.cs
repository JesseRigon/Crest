using Grove.Workflows.Attributes;

namespace Grove.Workflows.Contents.Activities;

[Activity("Grove.Content", "Content", "Triggered when a content item has been translated, once the translation commits.")]
public class ContentLocalized : ContentEventTriggerBase;
