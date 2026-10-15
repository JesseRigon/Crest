using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Indexing;

namespace Grove.Indexing.DataMigrations;

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

            var shouldEnabled = await featuresManager.IsFeatureEnabledAsync("Grove.Search.Elasticsearch.Worker")
                || await featuresManager.IsFeatureEnabledAsync("Grove.Search.Lucene.Worker");

            if (!shouldEnabled)
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync(IndexingConstants.Feature.Worker);
        });

        return 1;
    }
}
