using Crest.Environment.Cache;
using Crest.Media.Core.Events;

namespace Crest.Media.Events;

internal sealed class SecureMediaFileStoreEventHandler : MediaEventHandlerBase
{
    private readonly ISignal _signal;

    public SecureMediaFileStoreEventHandler(ISignal signal)
    {
        _signal = signal;
    }

    public override Task MediaCreatedDirectoryAsync(MediaCreatedContext context)
    {
        if (context.Result)
        {
            SignalDirectoryChange();
        }

        return Task.CompletedTask;
    }

    public override Task MediaDeletedDirectoryAsync(MediaDeletedContext context)
    {
        if (context.Result)
        {
            SignalDirectoryChange();
        }

        return Task.CompletedTask;
    }

    private void SignalDirectoryChange() => _signal.DeferredSignalToken(nameof(SecureMediaPermissions));
}
