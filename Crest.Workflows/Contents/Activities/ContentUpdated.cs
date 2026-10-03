using Crest.Workflows.Attributes;

namespace Crest.Workflows.Contents.Activities;

[Activity("OrchardCore.Content", "Content", "Triggered when a content item draft has been updated.")]
public class ContentUpdated : ContentEventTriggerBase;