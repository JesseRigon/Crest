namespace Crest.Workflows.Runtime.Options;

/// <summary>
/// How <see cref="IStimulusSender"/> treats a stimulus that matched no trigger and no bookmark.
/// </summary>
public class StimulusSenderOptions
{
    /// <summary>
    /// Queue an untargeted (broadcast) stimulus that matched nothing, so a bookmark persisted
    /// moments later still catches it. Default true (upstream behaviour). A host that only sends
    /// stimuli after the bookmarks' transaction committed turns this off; targeted stimuli (a
    /// workflow instance, bookmark or activity instance named) are always queued.
    /// </summary>
    public bool QueueUnmatchedBroadcastStimuli { get; set; } = true;
}
