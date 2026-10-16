using Grove.Workflows.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grove.Workflows.Registry;

/// <summary>
/// Brings the shipped flows in once per shell (a shell is rebuilt whenever a feature is enabled):
/// the registries are filled, then <see cref="WorkflowFlowImporter.SyncAsync"/> imports what is
/// missing. It runs when the shell has activated (after its migrations committed), and on demand
/// the first time a content hook finds one of its system flows missing, so an item saved before
/// the activation's task has run (a feature's own seeding) still runs its system flows. A sync
/// that fails is logged as an error and retried on the next demand.
/// </summary>
public sealed class ShippedFlowsInstaller(ILogger<ShippedFlowsInstaller> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _installed;

    public bool Installed => _installed;

    /// <summary>Installs the shipped flows unless this shell already did, with the calling scope's services.</summary>
    public async Task EnsureAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (_installed)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_installed)
            {
                return;
            }

            await services.GetRequiredService<IRegistriesPopulator>().PopulateAsync(cancellationToken);

            // Shipped flows can only be read once the activity types they name are registered.
            await services.GetRequiredService<WorkflowFlowImporter>().SyncAsync(cancellationToken);
            _installed = true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The shipped workflow flows could not be installed: hooks whose system flows are missing refuse their saves.");
        }
        finally
        {
            _lock.Release();
        }
    }
}
