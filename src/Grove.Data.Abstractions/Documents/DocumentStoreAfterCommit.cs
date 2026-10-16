using System.Runtime.CompilerServices;

namespace Grove.Data.Documents;

/// <summary>
/// Work that touches state outside the unit's session (a cache entry, a signal) and must happen
/// only once the unit commits: a cancelled unit (a refused save, a preview) runs none of it.
/// </summary>
public static class DocumentStoreAfterCommit
{
    private static readonly ConditionalWeakTable<IDocumentStore, List<Func<Task>>> s_work = [];

    /// <summary>Runs <paramref name="work"/> after the store commits successfully, in the order queued; never when the unit is cancelled.</summary>
    public static void AfterCommit(this IDocumentStore store, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(work);

        if (s_work.TryGetValue(store, out var queued))
        {
            queued.Add(work);
            return;
        }

        queued = [work];
        s_work.Add(store, queued);

        // The store keeps one delegate per type: this one runs everything queued on the store.
        store.AfterCommitSuccess<AfterCommitWork>(async () =>
        {
            s_work.Remove(store);
            foreach (var item in queued)
            {
                await item();
            }
        });
    }

    private sealed class AfterCommitWork;
}
