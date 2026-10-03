using Crest.Workflows.Attributes;

namespace Crest.Workflows.Contents.Activities;

[Activity("OrchardCore.Content", "Content", "Triggered when a content item has been deleted.")]
public class ContentDeleted : ContentEventTriggerBase;