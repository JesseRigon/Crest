using Medallion.Threading;

namespace Crest.Workflows.Units;

/// <summary>
/// <inheritdoc cref="IWorkflowObjectLock"/> Scoped: the keys held are the unit's (one shell
/// scope), so a service the unit calls again for the same object - the posting flow's
/// activity, the hook's child instance - re-enters instead of waiting on itself. A different
/// unit (another request, a background job's scope) waits.
/// </summary>
public sealed class WorkflowObjectLock(IDistributedLockProvider locks) : IWorkflowObjectLock
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);

    public async Task<IAsyncDisposable> LockAsync(string objectId, CancellationToken cancellationToken = default)
    {
        var key = $"crest-object:{objectId}";
        if (!_held.Add(key))
        {
            return Reentrant.Instance;
        }

        var handle = await locks.AcquireLockAsync(key, Timeout, cancellationToken);
        return new Release(handle, _held, key);
    }

    private sealed class Reentrant : IAsyncDisposable
    {
        public static readonly Reentrant Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Release(IDistributedSynchronizationHandle handle, HashSet<string> held, string key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            held.Remove(key);
            await handle.DisposeAsync();
        }
    }
}
