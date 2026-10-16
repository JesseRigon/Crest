namespace Grove.Contents.Management.Handlers;

/// <summary>
/// The points of a content item's lifecycle (docs/content-engine.md › Behaviour is workflows).
/// Each point has a hook, run inside the saving unit of work before the item is stored, which
/// may refuse; and a trigger, raised once the unit commits. Both carry the item's previous state
/// and its new one.
/// </summary>
public static class ContentLifecyclePoints
{
    public const string Creating = "creating";
    public const string Updating = "updating";
    public const string Validating = "validating";
    public const string SavingDraft = "saving-draft";
    public const string Publishing = "publishing";
    public const string Unpublishing = "unpublishing";
    public const string Removing = "removing";
    public const string Cloning = "cloning";
    public const string Importing = "importing";
    public const string Localizing = "localizing";

    public static IReadOnlyList<string> All { get; } =
    [
        Creating,
        Updating,
        Validating,
        SavingDraft,
        Publishing,
        Unpublishing,
        Removing,
        Cloning,
        Importing,
        Localizing,
    ];

    public static bool IsPoint(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);
}

/// <summary>One refusal of a hook: the field path it is about (<c>FieldSet.Field</c>, <c>FieldSet</c>,
/// or <c>""</c> for the item) and the message.</summary>
public sealed record ContentLifecycleError(string Path, string Message);

/// <summary>
/// Where an item's previous state comes from at a lifecycle point. Usually the stored version
/// (<see cref="Stored"/>): the document as the store holds it, read on demand past the session's
/// identity map, so in-memory changes do not show. A copy (<see cref="Copy"/>) where the stored row
/// is not the previous state: a version being merged, a clone's or translation's source.
/// </summary>
public sealed class ContentPreviousState
{
    private ContentPreviousState(long documentId, ContentItem copy)
    {
        DocumentId = documentId;
        CopyOfItem = copy;
    }

    /// <summary>No previous state: the item is being created.</summary>
    public static ContentPreviousState None { get; } = new(0, null);

    /// <summary>The stored document with this id.</summary>
    public static ContentPreviousState Stored(long documentId) => documentId > 0 ? new(documentId, null) : None;

    /// <summary>The stored version of this item, or none when it has never been stored.</summary>
    public static ContentPreviousState Of(ContentItem contentItem) => contentItem is null ? None : Stored(contentItem.Id);

    /// <summary>A copy taken in memory (<see cref="ContentItem.Snapshot"/>).</summary>
    public static ContentPreviousState Copy(ContentItem copy) => copy is null ? None : new(0, copy);

    public long DocumentId { get; }

    public ContentItem CopyOfItem { get; }
}

/// <summary>
/// What a lifecycle hook or trigger sees: the point, the item's new state, and its previous state
/// read when first asked for (null when it is being created). A hook refuses with <see cref="Fail"/>.
/// </summary>
public sealed class ContentLifecycleContext(string point, ContentItem contentItem, Func<Task<ContentItem>> previousContentItem)
{
    private readonly List<ContentLifecycleError> _errors = [];
    private Task<ContentItem> _previous;

    public ContentLifecycleContext(string point, ContentItem contentItem, ContentItem previousContentItem)
        : this(point, contentItem, () => Task.FromResult(previousContentItem))
    {
    }

    public string Point { get; } = point;

    public ContentItem ContentItem { get; } = contentItem;

    /// <summary>The item's state before the change, read once and kept for the point.</summary>
    public Task<ContentItem> GetPreviousContentItemAsync() => _previous ??= previousContentItem();

    public IReadOnlyList<ContentLifecycleError> Errors => _errors;

    public void Fail(string path, string message) => _errors.Add(new(path ?? string.Empty, message));
}

/// <summary>
/// A module's part in the content lifecycle. <see cref="HookAsync"/> runs inside the saving unit
/// of work, before the item is stored, and refuses through <see cref="ContentLifecycleContext.Fail"/>:
/// unlike a content handler's, a refusal is not swallowed - the operation is aborted and the unit
/// cancelled. <see cref="TriggerAsync"/> runs once the item is stored; what it starts must wait for
/// the unit to commit (the workflow stimulus queue does). A hook may read the previous state when
/// it runs; a trigger reads it only when <see cref="NeedsPreviousAfterStoreAsync"/> said so, since
/// once the item is stored its stored version is the new state.
/// </summary>
public interface IContentLifecycleHandler
{
    Task HookAsync(ContentLifecycleContext context);

    Task TriggerAsync(ContentLifecycleContext context);

    /// <summary>Whether <see cref="TriggerAsync"/> reads the previous state at this point for items
    /// of this type: the lifecycle then reads it before the item is stored.</summary>
    ValueTask<bool> NeedsPreviousAfterStoreAsync(string point, string contentType);
}

/// <summary>
/// Runs the lifecycle handlers at a point. The content manager calls it at every point; a module
/// that owns a point of its own (localizing) calls it too. The previous state is read only when a
/// handler asks for it. Hooks are not re-entered for an item whose hooks are already running in
/// this unit: a hook's own write to the item it is about runs no hooks again.
/// </summary>
public interface IContentLifecycle
{
    /// <summary>Runs the point's hooks; throws <see cref="ContentLifecycleRefusedException"/> when
    /// one refuses, after cancelling the unit of work. Called before the item is stored.</summary>
    Task HookAsync(string point, ContentItem contentItem, ContentPreviousState previous);

    /// <summary>Runs the point's hooks and returns their refusals instead of throwing (the
    /// validating point, whose refusals join the item's validation errors).</summary>
    Task<IReadOnlyList<ContentLifecycleError>> CheckAsync(string point, ContentItem contentItem, ContentPreviousState previous);

    /// <summary>Raises the point's triggers once the item is stored. The previous state is the one
    /// the point's hook captured; <paramref name="previous"/> serves a point that ran no hook.</summary>
    Task TriggerAsync(string point, ContentItem contentItem, ContentPreviousState previous);
}

/// <summary>
/// A lifecycle hook refused the operation. Its errors are keyed by field path; an
/// <see cref="InvalidOperationException"/>, so API layers report it as the error contract's 400.
/// </summary>
public sealed class ContentLifecycleRefusedException(string point, IReadOnlyList<ContentLifecycleError> errors)
    : InvalidOperationException(string.Join(" ", errors.Select(error => error.Message)))
{
    public string Point { get; } = point;

    public IReadOnlyList<ContentLifecycleError> Errors { get; } = errors;
}

/// <summary>A system flow's refusal of an item (docs/content-engine.md › Behaviour is workflows): the message and the field path it is about.</summary>
public sealed record ContentFieldRefusal(string Error, string Field);
