using Grove.Workflows.Runtime.Entities;
using Grove.Workflows.Runtime.Filters;

namespace Grove.Workflows.Runtime.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="ITriggerStore"/> interface.
/// </summary>
public static class StoredTriggerStoreExtensions
{
    /// <summary>
    /// Finds all triggers that match the specified stimulus.
    /// </summary>
    public static ValueTask<IEnumerable<StoredTrigger>> FindManyByStimulusHashAsync(this ITriggerStore triggerStore, string stimulusHash, CancellationToken cancellationToken = default)
    {
        var filter = new TriggerFilter
        {
            Hash = stimulusHash
        };
        return triggerStore.FindManyAsync(filter, cancellationToken);
    }
}