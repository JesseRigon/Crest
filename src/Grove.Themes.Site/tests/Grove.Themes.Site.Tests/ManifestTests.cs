using System.Reflection;
using Grove.Display.Management.Manifest;
using Xunit;

namespace Grove.Themes.Site.Tests;

// Smoke test only - establishes the .Tests project convention for this subproject.
// Real localization coverage (translated content rendering) lives in the Playwright
// suite (localization-smoke-site.js), since it needs a running tenant to verify.
public sealed class ManifestTests
{
    [Fact]
    public void ThemeManifestDeclaresTheExpectedThemeId()
    {
        var attribute = typeof(global::Grove.Themes.Site.Startup).Assembly
            .GetCustomAttribute<ThemeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Grove.Themes.Site", attribute!.Id);
    }
}
