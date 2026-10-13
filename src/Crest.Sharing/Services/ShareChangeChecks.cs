using Crest.Access;

namespace Crest.Sharing.Services;

/// <summary>
/// Deleting and moving (docs/sharing.md, requirements 68-70), for any kind: Manager on the object
/// and on everything below it, the objects the caller cannot see included; moving also Editor on
/// the destination. Each object is resolved live, so the check agrees with the decision. A
/// refusal says only that the container holds items the caller cannot delete or move: it never
/// names or counts them, and an object the caller cannot view is "not found".
/// </summary>
public sealed class ShareChangeChecks(
    ICallerContextAccessor callers,
    ISharingResolver resolver,
    ShareableKinds kinds) : IShareChangeChecks
{
    /// <summary>The most objects below one object checked; a larger tree is refused rather than
    /// partly checked.</summary>
    public const int MaxObjects = 10_000;

    private const int MaxDepth = 64;

    public const string Manage = "Sharing.Manager";

    public Task RequireDeleteAsync(ShareableRef target, CancellationToken cancellationToken = default) =>
        RequireManagerBelowAsync(Caller(), target, "delete", cancellationToken);

    public async Task RequireMoveAsync(ShareableRef target, ShareableRef destination, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        await RequireManagerBelowAsync(caller, target, "move", cancellationToken);
        if (caller.IsSuperUser)
        {
            return;
        }

        var there = await resolver.ResolveAsync(caller, destination, cancellationToken);
        if (!there.Allows(AccessLevel.Editor))
        {
            throw new AccessDeniedException(
                "Sharing.Editor",
                there.Allows(AccessLevel.Viewer) ? "You may not add items to the destination." : "Not found.");
        }
    }

    private CallerContext Caller() =>
        callers.Current ?? throw new AccessDeniedException(Manage, "A change was asked with no caller: the access gate did not run.");

    private async Task RequireManagerBelowAsync(CallerContext caller, ShareableRef target, string verb, CancellationToken cancellationToken)
    {
        if (caller.IsSuperUser)
        {
            return;
        }

        var own = await resolver.ResolveAsync(caller, target, cancellationToken);
        if (!own.Allows(AccessLevel.Manager))
        {
            throw new AccessDeniedException(Manage, own.Allows(AccessLevel.Viewer) ? $"You may not {verb} this item." : "Not found.");
        }

        var refusal = $"The folder contains items you cannot {verb}.";
        var seen = new HashSet<ShareableRef> { target };
        var level = new List<ShareableRef> { target };
        for (var depth = 0; depth < MaxDepth && level.Count > 0; depth++)
        {
            var next = new List<ShareableRef>();
            foreach (var parent in level)
            {
                foreach (var child in await kinds.ChildrenAsync(parent, cancellationToken))
                {
                    if (!seen.Add(child))
                    {
                        continue;
                    }

                    if (seen.Count > MaxObjects || !(await resolver.ResolveAsync(caller, child, cancellationToken)).Allows(AccessLevel.Manager))
                    {
                        throw new AccessDeniedException(Manage, refusal);
                    }

                    next.Add(child);
                }
            }

            level = next;
        }

        // A tree deeper than can be checked is refused: nothing below may go unchecked.
        if (level.Count > 0)
        {
            throw new AccessDeniedException(Manage, refusal);
        }
    }
}
