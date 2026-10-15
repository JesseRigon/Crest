namespace Grove.Sharing;

/// <summary>
/// Implemented by a module that places objects of another kind in its containers
/// (docs/sharing.md, requirement 74: a content item filed in a client folder): the objects a
/// container holds beside its own kind's children. Sharing walks them with the kind's
/// <see cref="IShareableTree"/> children, so a change on the folder recalculates what is filed in
/// it, and deleting or moving the folder checks them (requirements 69-70).
/// </summary>
public interface IShareableChildSource
{
    /// <summary>The objects filed directly in <paramref name="parent"/>; empty for none.</summary>
    Task<IReadOnlyList<ShareableRef>> ChildrenAsync(ShareableRef parent, CancellationToken cancellationToken = default);
}

/// <summary>
/// The access checks of deleting and moving, for any shareable kind (docs/sharing.md,
/// requirements 69-70), as the gated caller. Deleting needs Manager on the object and on
/// everything below it, including what the caller cannot see; moving needs the same over
/// everything moved plus Editor on the destination. A refusal never names or counts what failed:
/// the object the caller cannot see does not exist for them (68). Tenant administrators pass.
/// Refusals throw <c>AccessDeniedException</c>; the module performs the change itself after.
/// </summary>
public interface IShareChangeChecks
{
    Task RequireDeleteAsync(ShareableRef target, CancellationToken cancellationToken = default);

    Task RequireMoveAsync(ShareableRef target, ShareableRef destination, CancellationToken cancellationToken = default);
}

/// <summary>One comment on a shareable object (requirements 71-72).</summary>
public sealed record ShareComment(
    string Id,
    string AuthorId,
    string? AuthorName,
    string Text,
    DateTimeOffset CreatedUtc,
    string? ReplyTo = null);

/// <summary>
/// Where comments on some objects are kept (requirement 72). By default they are stored with the
/// object's sharing metadata; a kind (or a file type: a word-processing document keeps them in
/// its own format) registers a store that <see cref="Handles"/> its objects instead, and the
/// first store that handles an object, by feature priority, keeps its comments.
/// </summary>
public interface IShareCommentStore
{
    bool Handles(ShareableObject target);

    Task<IReadOnlyList<ShareComment>> GetAsync(ShareableObject target, CancellationToken cancellationToken = default);

    Task AddAsync(ShareableObject target, ShareComment comment, CancellationToken cancellationToken = default);

    /// <summary>False when there was no such comment.</summary>
    Task<bool> RemoveAsync(ShareableObject target, string commentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Comments on any shareable object, as the gated caller (requirements 71-72): a Viewer reads
/// them, a Commenter adds them (and that is all a Commenter can do), the author removes their own
/// and a Manager removes any. An object the caller cannot view does not exist for them.
/// </summary>
public interface IShareComments
{
    Task<IReadOnlyList<ShareComment>> ListAsync(ShareableRef target, CancellationToken cancellationToken = default);

    Task<ShareComment> AddAsync(ShareableRef target, string text, string? replyTo = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(ShareableRef target, string commentId, CancellationToken cancellationToken = default);
}
