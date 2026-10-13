using Crest.Access;

namespace Crest.Sharing.Services;

/// <summary>
/// Access indicators for lists (docs/sharing.md, requirement 98), read from the access index:
/// public when any path is public, shared when any path grants someone besides the owner,
/// private otherwise. Only objects the caller may view are answered; an object never calculated
/// is left out (it is in no list either, requirement 82).
/// </summary>
public sealed class ShareIndicators(ICallerContextAccessor callers, ISharingResolver resolver, IShareAccessStore store) : IShareIndicators
{
    public const int MaxTargets = 500;

    public async Task<IReadOnlyDictionary<ShareableRef, ShareIndicator>> GetAsync(IReadOnlyCollection<ShareableRef> targets, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<ShareableRef, ShareIndicator>();
        if (callers.Current is not { } caller)
        {
            return result;
        }

        foreach (var target in targets.Distinct().Take(MaxTargets))
        {
            var paths = await store.GetPathsAsync(target);
            if (paths.Count == 0)
            {
                continue;
            }

            if (!caller.IsSuperUser && !(await resolver.ResolveAsync(caller, target, cancellationToken)).Allows(AccessLevel.Viewer))
            {
                continue;
            }

            result[target] = Of(paths);
        }

        return result;
    }

    public static ShareIndicator Of(IEnumerable<ShareAccessDocument> paths)
    {
        var list = paths.Where(path => !path.IsInactive).ToList();
        return list.Any(path => path.IsPublic) ? ShareIndicator.Public
            : list.Any(path => path.IsShared) ? ShareIndicator.Shared
            : ShareIndicator.Private;
    }
}
