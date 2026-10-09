using Microsoft.Extensions.DependencyInjection;
using Crest.Data.Migration;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;
using Crest.Indexing.Core;

namespace Crest.Indexing.DataMigrations;

internal sealed class WorkerFeatureMigrations : DataMigration
{
    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var featuresManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

            if (await featuresManager.IsFeatureEnabledAsync(IndexingConstants.Feature.Worker))
            {
                return;
            }

            var shouldEnabled = await featuresManager.IsFeatureEnabledAsync("Crest.Search.Elasticsearch.Worker")
                || await featuresManager.IsFeatureEnabledAsync("Crest.Search.Lucene.Worker");

            if (!shouldEnabled)
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync(IndexingConstants.Feature.Worker);
        });

        return 1;
    }
}
