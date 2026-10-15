using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Entities;
using Grove.Environment.Options;
using Grove.Environment.Shell;
using Grove.Infrastructure.Settings;
using Grove.Tests.Apis.Context;
using Grove.Access.Users;
using Grove.Access.Users.Models;

namespace Grove.Tests.Modules.Grove.Users;

public class RegistrationOptionsMonitorTests
{
    [Fact]
    public async Task RequestUpdate_ShouldRefreshRegistrationOptionsWithoutReleasingTenant()
    {
        using var context = new SiteContext()
            .WithRecipe(TestRecipes.Blank);

        await context.InitializeAsync();

        await context.UsingTenantScopeAsync(async scope =>
        {
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var availableFeatures = await shellFeaturesManager.GetAvailableFeaturesAsync();
            var featuresToEnable = availableFeatures.Where(feature => feature.Id == UserConstants.Features.UserRegistration);

            await shellFeaturesManager.EnableFeaturesAsync(featuresToEnable, force: true);
        });

        await context.WaitForDeferredTasksAsync(CancellationToken.None);

        long shellContextTicks = 0;

        await context.UsingTenantScopeAsync(scope =>
        {
            shellContextTicks = scope.ShellContext.UtcTicks;

            var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<RegistrationOptions>>().CurrentValue;

            Assert.False(options.UsersMustValidateEmail);
            Assert.False(options.UsersAreModerated);

            return Task.CompletedTask;
        });

        await context.UsingTenantScopeAsync(async scope =>
        {
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var notifier = scope.ServiceProvider.GetRequiredService<IOptionsUpdateNotifier>();

            var site = await siteService.LoadSiteSettingsAsync();
            site.Put(new RegistrationSettings
            {
                UsersMustValidateEmail = true,
                UsersAreModerated = true,
            });

            notifier.RequestUpdate<RegistrationOptions>();

            await siteService.UpdateSiteSettingsAsync(site);
        });

        await context.WaitForDeferredTasksAsync(CancellationToken.None);

        await context.UsingTenantScopeAsync(scope =>
        {
            Assert.Equal(shellContextTicks, scope.ShellContext.UtcTicks);

            var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<RegistrationOptions>>().CurrentValue;

            Assert.True(options.UsersMustValidateEmail);
            Assert.True(options.UsersAreModerated);

            return Task.CompletedTask;
        });
    }
}
