using Crest.Workflows.Attributes;

namespace Crest.Workflows.Contents.Activities;

[Activity("OrchardCore.Content", "Content", "Triggered when a content item draft has been unpublished.")]
public class ContentUnpublished : ContentEventTriggerBase;