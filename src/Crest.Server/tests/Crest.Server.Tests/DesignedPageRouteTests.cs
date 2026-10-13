using Crest.Templates.Services;
using Xunit;

namespace Crest.Server.Tests;

// Designed pages (docs/blazor-display.md decision 9): a page record's route pattern, matched
// against a path within its bucket, its values handed to the tree as Model.Route.
public sealed class DesignedPageRouteTests
{
    [Theory]
    [InlineData("/landing", "/Landing/", true)]
    [InlineData("/landing", "/landing/more", false)]
    [InlineData("/offers/{id}", "/offers/42", true)]
    [InlineData("/offers/{id}", "/offers", false)]
    [InlineData("/offers/{id?}", "/offers", true)]
    [InlineData("/docs/{*path}", "/docs/a/b", true)]
    public void RoutesMatchByLiteralAndParameter(string pattern, string path, bool matches) =>
        Assert.Equal(matches, DesignedPageRoutes.TryMatch(pattern, path, out _));

    [Fact]
    public void ValuesAreReadFromThePath()
    {
        Assert.True(DesignedPageRoutes.TryMatch("/offers/{id:int}/{*rest}", "/offers/42/a%20b/c", out var values));

        Assert.Equal("42", values["id"]);
        Assert.Equal("a b/c", values["rest"]);
    }

    [Theory]
    [InlineData("/offers/{id}", true)]
    [InlineData("/offers/{id}/{id}", false)]
    [InlineData("/docs/{*path}/more", false)]
    [InlineData("/bad{segment", false)]
    public void PatternsAreValidated(string pattern, bool valid) => Assert.Equal(valid, DesignedPageRoutes.IsValid(pattern, out _));
}
