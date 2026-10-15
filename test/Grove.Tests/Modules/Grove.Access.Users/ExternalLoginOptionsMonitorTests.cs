using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Entities;
using Grove.Environment.Options;
using Grove.Environment.Shell;
using Grove.Infrastructure.Settings;
using Grove.Tests.Apis.Context;
using Grove.Access.Users.Models;

namespace Grove.Tests.Modules.Grove.Users;

public class ExternalLoginOptionsMonitorTests
{
    [Fact]
    public async Task RequestUpdate_ShouldRefreshExternalLoginOptionsWithoutReleasingTenant()
    {
        using var context = new SiteContext()
            .WithRecipe(TestRecipes.Blank);

        await context.InitializeAsync();

        await context.UsingTenantScopeAsync(async scope =>
        {
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var availableFeatures = await shellFeaturesManager.GetAvailableFeaturesAsync();
            var featuresToEnable = availableFeatures.Where(feature => feature.Id == "Grove.Integrations.GitHub.Authentication");

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
