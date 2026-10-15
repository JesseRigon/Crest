using Grove.Display.Management.Components;
using Xunit;

namespace Grove.Infrastructure.Server.Tests;

// Decision 12 (docs/blazor-display.md): a tree's composite version is its ETag, compared
// without evaluating; the shared cache holds only what one permission set sees.
public sealed class DisplayTreeVersionsTests
{
    [Fact]
    public async Task TheVersionChangesWithAnyPartTheContentOrTheScope()
    {
        var versions = new DisplayTreeVersions();
        var part = new Part("a");
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await versions.ComputeAsync([part], ["page", "/about", "viewer"], cancellationToken);

        Assert.Equal(first, await versions.ComputeAsync([part], ["page", "/about", "viewer"], cancellationToken));
        Assert.NotEqual(first, await versions.ComputeAsync([part], ["page", "/about", "other viewer"], cancellationToken));

        part.Version = "b";
        var second = await versions.ComputeAsync([part], ["page", "/about", "viewer"], cancellationToken);
        Assert.NotEqual(first, second);

        versions.BumpContent();
        Assert.NotEqual(second, await versions.ComputeAsync([part], ["page", "/about", "viewer"], cancellationToken));
    }

    [Fact]
    public async Task AnotherTenantStartNeverMatchesAnOldVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.NotEqual(
            await new DisplayTreeVersions().ComputeAsync([], ["page"], cancellationToken),
            await new DisplayTreeVersions().ComputeAsync([], ["page"], cancellationToken));
    }

    [Theory]
    [InlineData("\"v1\"", true)]
    [InlineData("W/\"v1\"", true)]
    [InlineData("\"v0\", \"v1\"", true)]
    [InlineData("*", true)]
    [InlineData("\"v2\"", false)]
    [InlineData(null, false)]
    public void IfNoneMatchIsComparedByTag(string? header, bool matches) => Assert.Equal(matches, DisplayTreeVersions.Matches(header, "v1"));

    [Fact]
    public void TheCacheReturnsWhatItHolds()
    {
        using var cache = new DisplayTreeCache();
        cache.Set("v1", "tree", 4);

        Assert.True(cache.TryGet<string>("v1", out var tree));
        Assert.Equal("tree", tree);
        Assert.False(cache.TryGet<string>("v2", out _));
    }

    private sealed class Part(string version) : IDisplayTreeVersionPart
    {
        public string Version { get; set; } = version;

        public ValueTask<string?> GetVersionAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<string?>(Version);
    }
}
