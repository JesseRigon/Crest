using System.Runtime.CompilerServices;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Security.RateLimits;
using Grove.Security.RateLimits.Models;

namespace Grove.Tests.Modules.Grove.RateLimits;

// A partitioned limiter runs a timer until it is disposed, and its partition callbacks reach the shell's
// services. The shell's limiters are disposed with the shell's container, so a released shell is not kept
// alive by a running timer.
public class ShellRateLimiterTests
{
    [Fact]
    public async Task Disposing_the_shell_disposes_its_limiters()
    {
        var provider = BuildShellServices();
        var limiter = provider.GetRequiredService<ShellRateLimiter>().Options.GlobalLimiter;
        Assert.NotNull(limiter);

        await provider.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => limiter.AttemptAcquire(new DefaultHttpContext()));
    }

    [Fact]
    public async Task A_disposed_shell_is_collectable()
    {
        var shell = await BuildUseAndDisposeAsync();

        for (var i = 0; i < 5 && shell.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.False(shell.IsAlive, "A released shell's container is still reachable.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> BuildUseAndDisposeAsync()
    {
        var provider = BuildShellServices();
        var limiter = provider.GetRequiredService<ShellRateLimiter>().Options.GlobalLimiter;
        using (var lease = limiter.AttemptAcquire(new DefaultHttpContext { RequestServices = provider }))
        {
            Assert.True(lease.IsAcquired);
        }

        await provider.DisposeAsync();
        return new WeakReference(provider);
    }

    private static ServiceProvider BuildShellServices()
    {
        var services = new ServiceCollection();
        services.AddShellRateLimiter();
        services.AddTransient<IConfigureOptions<RateLimiterOptions>, RateLimiterOptionsConfigurations>();
        services.AddSingleton<IRateLimitPolicyStore>(new EmptyPolicyStore());
        return services.BuildServiceProvider();
    }

    private sealed class EmptyPolicyStore : IRateLimitPolicyStore
    {
        public ValueTask<RateLimitPolicy> FindByIdAsync(string policyId, PolicyVersion version) => ValueTask.FromResult<RateLimitPolicy>(null);

        public ValueTask<IReadOnlyCollection<RateLimitPolicy>> GetAllAsync(PolicyVersion version) => ValueTask.FromResult<IReadOnlyCollection<RateLimitPolicy>>([]);

        public ValueTask CreateAsync(RateLimitPolicy policy) => ValueTask.CompletedTask;

        public ValueTask UpdateAsync(RateLimitPolicy policy) => ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAsync(RateLimitPolicy policy) => ValueTask.FromResult(false);

        public ValueTask<bool> SetStatusAsync(string policyId, bool isEnabled) => ValueTask.FromResult(false);
    }
}
