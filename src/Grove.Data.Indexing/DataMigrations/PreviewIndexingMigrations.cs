using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Migration;
using Grove.Documents;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Data.Indexing;
using Grove.Data.Indexing.Models;
using YesSql;

namespace Grove.Data.Indexing.DataMigrations;

// This migration can be removed before releasing Grove 3.0.
internal sealed class PreviewIndexingMigrations : DataMigration
{
    private readonly ShellSettings _shellSettings;

    public PreviewIndexingMigrations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public int Create()
    {
        if (_shellSettings.IsInitializing())
        {
            return 1;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var store = scope.ServiceProvider.GetRequiredService<IDocumentManager<DictionaryDocument<IndexProfile>>>();
            var document = await store.GetOrCreateImmutableAsync();

            if (document.Records.Count == 0)
            {
                // If there are no records, we can safely return.
                return;
            }

            var session = scope.ServiceProvider.GetRequiredService<ISession>();

            foreach (var record in document.Records)
            {
                await session.SaveAsync(record.Value);
            }

            await session.FlushAsync();
        });

        return 1;
    }
}
