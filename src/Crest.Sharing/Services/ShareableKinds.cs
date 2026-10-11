using Crest.Environment.Extensions.Features;
using Microsoft.Extensions.Logging;

namespace Crest.Sharing.Services;

/// <summary>
/// The shareable kinds in force: one per name, the one shipped by the feature with the highest
/// priority, so a module (a third party's included) replaces another's kind for a name by
/// declaring a higher priority. Two kinds for one name at the same priority are a
/// configuration conflict: the name answers nothing (no access) and the conflict is logged.
/// </summary>
public sealed class ShareableKinds
{
    private readonly Dictionary<string, IShareableKind?> _byName = new(StringComparer.Ordinal);

    public ShareableKinds(IEnumerable<IShareableKind> kinds, IProviderPriority? priority = null, ILogger<ShareableKinds>? logger = null)
    {
        foreach (var group in kinds.GroupBy(kind => kind.Name, StringComparer.Ordinal))
        {
            var ranked = group.GroupBy(kind => priority?.PriorityOf(kind.GetType()) ?? 0).OrderByDescending(rank => rank.Key).First().ToList();
            if (ranked.Count > 1)
            {
                logger?.LogError("Shareable kind '{Kind}' is registered by {Types} at the same priority; give one feature a higher priority. The kind is not shareable until then.", group.Key, string.Join(", ", ranked.Select(kind => kind.GetType().Name)));
                _byName[group.Key] = null;
                continue;
            }

            _byName[group.Key] = ranked[0];
        }
    }

    /// <summary>The kinds in force (a conflicted name has none).</summary>
    public IEnumerable<IShareableKind> All => _byName.Values.OfType<IShareableKind>();

    public IShareableKind? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>The kind in force that recognises <paramref name="resource"/>; a conflicted name's
    /// resources are claimed with no kind, so they are denied rather than decided elsewhere.</summary>
    public bool TryIdentify(IEnumerable<IShareableKind> registered, object resource, out ShareableRef target, out IShareableKind? kind)
    {
        foreach (var candidate in registered)
        {
            if (candidate.TryIdentify(resource, out var id))
            {
                target = new ShareableRef(candidate.Name, id);
                kind = Find(candidate.Name);
                return true;
            }
        }

        target = default;
        kind = null;
        return false;
    }
}
