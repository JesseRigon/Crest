using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Profiling;
using StackExchange.Profiling.Storage;

namespace Grove.Tests.Modules.Diagnostics.MiniProfiler;

// Every request is profiled and the profiles live in the tenant's memory cache, so how long
// they are kept bounds how much memory the profiler holds.
public class ProfileRetentionTests
{
    [Fact]
    public void Profiles_are_kept_for_the_short_retention_only()
    {
        var services = new ServiceCollection();
        new global::Grove.Diagnostics.MiniProfiler.Startup().ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        var storage = Assert.IsType<MemoryCacheStorage>(provider.GetRequiredService<IOptions<MiniProfilerOptions>>().Value.Storage);

        Assert.Equal(global::Grove.Diagnostics.MiniProfiler.Startup.ProfileRetention, storage.CacheDuration);
        Assert.True(storage.CacheDuration <= TimeSpan.FromMinutes(5));
    }
}
