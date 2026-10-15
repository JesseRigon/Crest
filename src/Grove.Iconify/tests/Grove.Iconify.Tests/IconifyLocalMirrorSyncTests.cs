using Grove.Iconify;
using Xunit;

namespace Grove.Iconify.Tests;

// The local mirror is built by a sync (the host bugs list, #7): a fake git runner stands in for
// the icon-sets clone, so the test needs no network.
public sealed class IconifyLocalMirrorSyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "iconify-mirror-" + Guid.NewGuid().ToString("N"), "IconifyCache");

    [Fact]
    public async Task SyncPopulatesTheMirror()
    {
        var git = new FakeGitRunner();
        var store = new IconifyLocalMirrorStore(new FixedPath(_root), git);

        var status = await store.SyncAsync(TestContext.Current.CancellationToken);

        Assert.True(status.IsAvailable, status.LastError);
        Assert.Equal(1, status.PrefixCount);
        Assert.Contains(git.Calls, call => call.StartsWith("clone ", StringComparison.Ordinal));

        var icon = await store.ResolveAsync(IconifyIconProviderSettings.Default, "demo", "home", TestContext.Current.CancellationToken);
        Assert.NotNull(icon);
        Assert.Contains("<path d=\"M0 0\"/>", icon.SvgMarkup);
    }

    public void Dispose()
    {
        var parent = Path.GetDirectoryName(_root)!;
        if (Directory.Exists(parent))
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    private sealed class FixedPath(string rootPath) : IIconifyLocalMirrorPathProvider
    {
        public string RootPath => rootPath;
    }

    // "clone ... <target>" writes a one-collection checkout into the target; every other
    // command succeeds without effect.
    private sealed class FakeGitRunner : IIconifyGitRunner
    {
        public List<string> Calls { get; } = [];

        public async Task RunAsync(string arguments, string workingDirectory, CancellationToken cancellationToken)
        {
            Calls.Add(arguments);
            if (!arguments.StartsWith("clone ", StringComparison.Ordinal))
            {
                return;
            }

            var target = arguments.Split(' ')[^1];
            Directory.CreateDirectory(Path.Combine(target, "json"));
            await File.WriteAllTextAsync(Path.Combine(target, "collections.json"), """{"demo":{"name":"Demo","total":1}}""", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(target, "json", "demo.json"), """{"prefix":"demo","icons":{"home":{"body":"<path d=\"M0 0\"/>"}}}""", cancellationToken);
        }
    }
}
