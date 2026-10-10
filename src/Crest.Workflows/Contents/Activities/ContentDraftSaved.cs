using Crest.Workflows.Attributes;

namespace Crest.Workflows.Contents.Activities;

[Activity("Crest.Content", "Content", "Triggered when a content item draft has been saved.")]
public class ContentDraftSaved : ContentEventTriggerBase;