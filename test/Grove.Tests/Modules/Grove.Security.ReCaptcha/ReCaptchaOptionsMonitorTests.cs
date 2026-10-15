using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Entities;
using Grove.Environment.Options;
using Grove.Environment.Shell;
using Grove.Security.ReCaptcha.Configuration;
using Grove.Infrastructure.Settings;
using Grove.Tests.Apis.Context;

namespace Grove.Tests.Modules.Grove.ReCaptcha;

public class ReCaptchaOptionsMonitorTests
{
    [Fact]
    public async Task RequestUpdate_ShouldRefreshReCaptchaOptionsWithoutReleasingTenant()
    {
        using var context = new SiteContext()
            .WithRecipe(TestRecipes.Blank);

        await context.InitializeAsync();

        await context.UsingTenantScopeAsync(async scope =>
        {
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var availableFeatures = await shellFeaturesManager.GetAvailableFeaturesAsync();
            var featuresToEnable = availableFeatures.Where(feature => feature.Id == "Grove.Security.ReCaptcha");

            await shellFeaturesManager.EnableFeaturesAsync(featuresToEnable, force: true);
        });

        await context.WaitForDeferredTasksAsync(CancellationToken.None);

        long shellContextTicks = 0;

        await context.UsingTenantScopeAsync(scope =>
        {
            shellContextTicks = scope.ShellContext.UtcTicks;

            var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<ReCaptchaSettings>>().CurrentValue;

            Assert.False(options.ConfigurationExists());

            return Task.CompletedTask;
        });

        await context.UsingTenantScopeAsync(async scope =>
        {
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var notifier = scope.ServiceProvider.GetRequiredService<IOptionsUpdateNotifier>();

            var site = await siteService.LoadSiteSettingsAsync();
            site.Put(new ReCaptchaSettings
            {
                SiteKey = "site-key",
                SecretKey = "secret-key",
            });

            notifier.RequestUpdate<ReCaptchaSettings>();

            await siteService.UpdateSiteSettingsAsync(site);
        });

        await context.WaitForDeferredTasksAsync(CancellationToken.None);

        await context.UsingTenantScopeAsync(scope =>
        {
            Assert.Equal(shellContextTicks, scope.ShellContext.UtcTicks);

            var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<ReCaptchaSettings>>().CurrentValue;

            Assert.True(options.ConfigurationExists());
            Assert.Equal("site-key", options.SiteKey);
            Assert.Equal("secret-key", options.SecretKey);

            return Task.CompletedTask;
        });
    }
}
