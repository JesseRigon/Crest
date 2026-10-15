using System.Reflection;
using Grove.DisplayManagement.Manifest;
using Xunit;

namespace Grove.SiteTheme.Tests;

// Smoke test only - establishes the .Tests project convention for this subproject.
// Real localization coverage (translated content rendering) lives in the Playwright
// suite (localization-smoke-site.js), since it needs a running tenant to verify.
public sealed class ManifestTests
{
    [Fact]
    public void ThemeManifestDeclaresTheExpectedThemeId()
    {
        var attribute = typeof(Grove.Themes.Grove.SiteTheme.Startup).Assembly
            .GetCustomAttribute<ThemeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Grove.SiteTheme", attribute!.Id);
    }
}
