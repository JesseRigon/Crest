using System.Reflection;
using OrchardCore.DisplayManagement.Manifest;
using Xunit;

namespace Crest.SiteTheme.Tests;

// Smoke test only - establishes the .Tests project convention for this subproject.
// Real localization coverage (translated content rendering) lives in the Playwright
// suite (localization-smoke-site.js), since it needs a running tenant to verify.
public sealed class ManifestTests
{
    [Fact]
    public void ThemeManifestDeclaresTheExpectedThemeId()
    {
        var attribute = typeof(OrchardCore.Themes.Crest.SiteTheme.Startup).Assembly
            .GetCustomAttribute<ThemeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Crest.SiteTheme", attribute!.Id);
    }
}
