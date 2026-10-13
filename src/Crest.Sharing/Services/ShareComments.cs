using Crest.Access;
using Crest.Environment.Extensions.Features;
using Crest.Modules;

namespace Crest.Sharing.Services;

/// <summary>
/// The default comment store (requirement 72): comments kept with the object's sharing metadata,
/// its share document. Used for every object no registered <see cref="IShareCommentStore"/>
/// handles.
/// </summary>
public sealed class ShareDocumentCommentStore(IShareStore store) : IShareCommentStore
{
    public bool Handles(ShareableObject target) => true;

    public async Task<IReadOnlyList<ShareComment>> GetAsync(ShareableObject target, CancellationToken cancellationToken = default) =>
        (await store.GetAsync(target.Ref))?.Comments ?? [];

    public async Task AddAsync(ShareableObject target, ShareComment comment, CancellationToken cancellationToken = default)
    {
        var document = await store.GetAsync(target.Ref) ?? new ShareDocument { Kind = target.Ref.Kind, ObjectId = target.Ref.Id };
        document.Comments.Add(comment);
        await store.SaveAsync(document);
    }

    public async Task<bool> RemoveAsync(ShareableObject target, string commentId, CancellationToken cancellationToken = default)
    {
        var document = await store.GetAsync(target.Ref);
        if (document is null || document.Comments.RemoveAll(comment => comment.Id == commentId) == 0)
        {
            return false;
        }

        await store.SaveAsync(document);
        return true;
    }
}

/// <summary>
/// Comments on shareable objects (docs/sharing.md, requirements 71-72). Reading takes Viewer;
/// adding takes Commenter, the level that gives nothing else; removing takes being the author
/// (still a Commenter) or Manager. The store is the first registered <see cref="IShareCommentStore"/>
/// that handles the object, highest feature priority first (a file type keeping comments in its
/// own format), else the object's share document. Adding and removing are audited.
/// </summary>
public sealed class ShareComments(
    ICallerContextAccessor callers,
    ISharingResolver resolver,
    ShareableKinds kinds,
    IShareStore store,
    IAccessAuditor auditor,
    IClock clock,
    IEnumerable<IShareCommentStore>? stores = null,
    IProviderPriority? priority = null) : IShareComments
{
    private readonly IShareCommentStore[] _stores = [.. (stores ?? [])
        .Select((store, index) => (store, index))
        .OrderByDescending(entry => priority?.PriorityOf(entry.store.GetType()) ?? 0)
        .ThenBy(entry => entry.index)
        .Select(entry => entry.store)];

    public const int MaxLength = 10_000;

    public async Task<IReadOnlyList<ShareComment>> ListAsync(ShareableRef target, CancellationToken cancellationToken = default)
    {
        var (item, _, _) = await RequireAsync(target, AccessLevel.Viewer, cancellationToken);
        return await StoreFor(item).GetAsync(item, cancellationToken);
    }

    public async Task<ShareComment> AddAsync(ShareableRef target, string text, string? replyTo = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("A comment needs text.", nameof(text));
        }

        if (text.Length > MaxLength)
        {
            throw new ArgumentException($"A comment is at most {MaxLength} characters.", nameof(text));
        }

        var (item, caller, _) = await RequireAsync(target, AccessLevel.Commenter, cancellationToken);
        var comment = new ShareComment(
            Guid.NewGuid().ToString("n"),
            caller.ActorId ?? throw new AccessDeniedException("Sharing.Commenter", "Only a signed-in person can comment."),
            caller.UserName,
            text.Trim(),
            new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc)),
            replyTo);
        await StoreFor(item).AddAsync(item, comment, cancellationToken);
        await RecordAsync(caller, "Sharing.AddComment", target, comment.Id, cancellationToken);
        return comment;
    }

    public async Task RemoveAsync(ShareableRef target, string commentId, CancellationToken cancellationToken = default)
    {
        var (item, caller, level) = await RequireAsync(target, AccessLevel.Commenter, cancellationToken);
        var commentStore = StoreFor(item);
        var comment = (await commentStore.GetAsync(item, cancellationToken)).FirstOrDefault(candidate => candidate.Id == commentId)
            ?? throw new KeyNotFoundException("No such comment.");
        if (comment.AuthorId != caller.ActorId && level < AccessLevel.Manager)
        {
            throw new AccessDeniedException("Sharing.Manager", "Only its author or a Manager can remove a comment.");
        }

        await commentStore.RemoveAsync(item, commentId, cancellationToken);
        await RecordAsync(caller, "Sharing.RemoveComment", target, commentId, cancellationToken);
    }

    private IShareCommentStore StoreFor(ShareableObject item)
    {
        if (_stores.FirstOrDefault(candidate => candidate.Handles(item)) is { } registered)
        {
            return registered;
        }

        // A kind may keep its objects' comments in their own metadata.
        return kinds.Find(item.Ref.Kind) is IShareCommentStore own && own.Handles(item) ? own : new ShareDocumentCommentStore(store);
    }

    private async Task<(ShareableObject Item, CallerContext Caller, AccessLevel Level)> RequireAsync(ShareableRef target, AccessLevel minimum, CancellationToken cancellationToken)
    {
        var caller = callers.Current ?? throw new AccessDeniedException("Sharing.Viewer", "Comments were asked with no caller: the access gate did not run.");
        var item = kinds.Find(target.Kind) is { } kind ? await kind.GetAsync(target.Id, cancellationToken) : null;
        if (item is null)
        {
            throw new AccessDeniedException("Sharing.Viewer", "Not found.");
        }

        if (caller.IsSuperUser)
        {
            return (item, caller, AccessLevel.Admin);
        }

        var resolution = await resolver.ResolveAsync(caller, target, cancellationToken);
        if (!resolution.Allows(minimum))
        {
            // An object the caller cannot view does not exist for them (requirement 68).
            throw new AccessDeniedException(
                minimum == AccessLevel.Viewer ? "Sharing.Viewer" : "Sharing.Commenter",
                resolution.Allows(AccessLevel.Viewer) ? "You may not comment on this item." : "Not found.");
        }

        return (item, caller, resolution.Level);
    }

    private Task RecordAsync(CallerContext caller, string operation, ShareableRef target, string detail, CancellationToken cancellationToken) =>
        auditor.RecordAsync(new AccessEvent(AccessEventKind.Execution, operation, caller, AccessVerdict.Allow, target.ToString(), caller.ScopeSignature, detail), cancellationToken);
}
