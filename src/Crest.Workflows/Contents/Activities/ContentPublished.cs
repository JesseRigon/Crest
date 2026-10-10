using Crest.Workflows.Attributes;

namespace Crest.Workflows.Contents.Activities;

[Activity("Crest.Content", "Content", "Triggered when a content item draft has been published.")]
public class ContentPublished : ContentEventTriggerBase;