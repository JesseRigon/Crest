using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Entities;
using Crest.Environment.Options;
using Crest.Environment.Shell;
using Crest.Settings;
using Crest.Tests.Apis.Context;
using Crest.Users.Models;

namespace Crest.Tests.Modules.Crest.Users;

public class ExternalLoginOptionsMonitorTests
{
    [Fact]
    public async Task RequestUpdate_ShouldRefreshExternalLoginOptionsWithoutReleasingTenant()
    {
        using var context = new SiteContext()
            .WithRecipe("SaaS");

        await context.InitializeAsync();

        await context.UsingTenantScopeAsync(async scope =>
        {
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var availableFeatures = await shellFeaturesManager.GetAvailableFeaturesAsync();
            var featuresToEnable = availableFeatures.Where(feature => feature.Id == "Crest.GitHub.Authentication");

            await shellFeaturesManager.EnableFeaturesAsync(featuresToEnable, force: true);
        });

        await context.WaitForDeferredTasksAsync(CancellationToken.None);

        long shellContextTicks = 0;

        await context.UsingTenantScopeAsync(scope =>
        {
            shellContextTicks = scope.ShellContext.UtcTicks;

            Assert.False(scope.ServiceProvider.GetRequiredService<IOptionsMonitor<ExternalLoginOptions>>().CurrentValue.UseExternalProviderIfOnlyOneDefined);

            return Task.CompletedTask;
        });

        await context.UsingTenantScopeAsync(async scope =>
        {
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var notifier = scope.ServiceProvider.GetRequiredService<IOptionsUpdateNotifier>();

            var site = await siteService.LoadSiteSettingsAsync();
            var settings = site.GetOrCreate<ExternalLoginSettings>();
            settings.UseExternalProviderIfOnlyOneDefined = true;
            site.Put(settings);

            notifier.RequestUpdate<ExternalLoginOptions>();

            await siteService.UpdateSiteSettingsAsync(site);
        });

        await context.WaitForDeferredTasksAsync(CancellationToken.None);

        await context.UsingTenantScopeAsync(scope =>
        {
            Assert.Equal(shellContextTicks, scope.ShellContext.UtcTicks);
            Assert.True(scope.ServiceProvider.GetRequiredService<IOptionsMonitor<ExternalLoginOptions>>().CurrentValue.UseExternalProviderIfOnlyOneDefined);

            return Task.CompletedTask;
        });
    }
}
