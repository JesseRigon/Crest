using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement;
using Crest.Entities;
using Crest.Environment.Options;
using Crest.Settings;
using Crest.Tests.Apis.Context;

namespace Crest.Tests.Modules.Crest.Settings;

public class ShapeRenderingOptionsMonitorTests
{
    [Fact]
    public async Task RequestUpdate_ShouldRefreshShapeRenderingOptionsWithoutReleasingTenant()
    {
        using var context = new SiteContext()
            .WithRecipe(TestRecipes.Blank);

        await context.InitializeAsync();

        long shellContextTicks = 0;

        await context.UsingTenantScopeAsync(scope =>
        {
            shellContextTicks = scope.ShellContext.UtcTicks;

            Assert.False(scope.ServiceProvider.GetRequiredService<IOptionsMonitor<ShapeRenderingOptions>>().CurrentValue.WriteShapeDebugInformation);

            return Task.CompletedTask;
        });

        await context.UsingTenantScopeAsync(async scope =>
        {
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var notifier = scope.ServiceProvider.GetRequiredService<IOptionsUpdateNotifier>();

            var site = await siteService.LoadSiteSettingsAsync();
            var settings = site.GetOrCreate<DebugSettings>();
            settings.WriteShapeDebugInformation = true;
            site.Put(settings);

            notifier.RequestUpdate<ShapeRenderingOptions>();

            await siteService.UpdateSiteSettingsAsync(site);
        });

        await context.WaitForDeferredTasksAsync(CancellationToken.None);

        await context.UsingTenantScopeAsync(scope =>
        {
            Assert.Equal(shellContextTicks, scope.ShellContext.UtcTicks);
            Assert.True(scope.ServiceProvider.GetRequiredService<IOptionsMonitor<ShapeRenderingOptions>>().CurrentValue.WriteShapeDebugInformation);

            return Task.CompletedTask;
        });
    }
}
