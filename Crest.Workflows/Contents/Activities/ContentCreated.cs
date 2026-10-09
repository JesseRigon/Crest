using Crest.Workflows.Attributes;

namespace Crest.Workflows.Contents.Activities;

[Activity("Crest.Content", "Content", "Triggered when a content item is created.")]
public class ContentCreated : ContentEventTriggerBase;